using AuditAI.Application.DTOs;
using AuditAI.Domain.Entities;
using AuditAI.Domain.Enums;
using AuditAI.Domain.Rules;

namespace AuditAI.Application.Services;

public sealed record ReconciliationOutcome(
    AuditStatus FinalStatus,
    RiskLevel RiskLevel,
    decimal TotalVarianceUsd,
    IReadOnlyList<AuditFinding> Findings);

public sealed class ReconciliationEngine
{
    public ReconciliationOutcome ReconcileInvoice(
        Guid documentId,
        ExtractedDocumentPayload extracted,
        PurchaseOrder? matchedPo)
    {
        var findings = new List<AuditFinding>();
        decimal totalVarianceUsd = 0m;

        var subtotal = extracted.Subtotal ?? 0m;
        var taxAmount = extracted.TaxAmount ?? 0m;
        var totalAmount = extracted.TotalAmount ?? 0m;

        // 1. Validación estructural del RUC ecuatoriano (13 dígitos)
        if (!EcuadorTaxRules.IsValidRuc(extracted.SupplierRuc, out var rucReason))
        {
            findings.Add(new AuditFinding
            {
                AuditDocumentId = documentId,
                Code = "RUC_INVALID_FORMAT",
                Category = "SupplierIdentity",
                Severity = FindingSeverity.Critical,
                Title = "RUC del proveedor inválido o ausente",
                Description = rucReason,
                ExpectedValue = "RUC válido de 13 dígitos (provincias 01-24/30 + sufijo 001)",
                ActualValue = extracted.SupplierRuc ?? "No detectado"
            });
        }

        // 2. Consistencia aritmética de líneas de factura vs Subtotal
        if (extracted.LineItems.Count > 0)
        {
            var itemsSum = Math.Round(extracted.LineItems.Sum(i => i.LineTotal), 2, MidpointRounding.AwayFromZero);
            var lineDiff = Math.Abs(itemsSum - subtotal);
            if (lineDiff > EcuadorTaxRules.RoundingToleranceUsd)
            {
                findings.Add(new AuditFinding
                {
                    AuditDocumentId = documentId,
                    Code = "LINE_ITEMS_SUM_MISMATCH",
                    Category = "Financial",
                    Severity = FindingSeverity.Warning,
                    Title = "Suma de ítems no cuadra con el subtotal declarado",
                    Description = $"La suma aritmética de las líneas de detalle (${itemsSum:F2}) difiere del subtotal de la factura (${subtotal:F2}).",
                    ExpectedValue = $"${itemsSum:F2}",
                    ActualValue = $"${subtotal:F2}",
                    FinancialImpactUsd = lineDiff
                });
            }
        }

        // 3. Verificación tributaria de IVA Ecuador (15%)
        var expectedIva = EcuadorTaxRules.CalculateExpectedIva(subtotal);
        var ivaDiff = Math.Round(taxAmount - expectedIva, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(ivaDiff) > EcuadorTaxRules.RoundingToleranceUsd)
        {
            var absIvaDiff = Math.Abs(ivaDiff);
            totalVarianceUsd += absIvaDiff;
            findings.Add(new AuditFinding
            {
                AuditDocumentId = documentId,
                Code = "IVA_CALCULATION_ERROR",
                Category = "TaxCompliance",
                Severity = FindingSeverity.Critical,
                Title = "Cálculo incorrecto de IVA (Tarifa vigente 15%)",
                Description = $"Sobre un subtotal de ${subtotal:F2}, el IVA del 15% debe ser ${expectedIva:F2}, pero la factura declara ${taxAmount:F2}.",
                ExpectedValue = $"${expectedIva:F2} (15%)",
                ActualValue = $"${taxAmount:F2}",
                FinancialImpactUsd = absIvaDiff
            });
        }

        // 4. Consistencia Subtotal + IVA == Total
        var expectedInvoiceTotal = Math.Round(subtotal + taxAmount, 2, MidpointRounding.AwayFromZero);
        var totalMathDiff = Math.Abs(totalAmount - expectedInvoiceTotal);
        if (totalMathDiff > EcuadorTaxRules.RoundingToleranceUsd)
        {
            findings.Add(new AuditFinding
            {
                AuditDocumentId = documentId,
                Code = "TOTAL_ARITHMETIC_ERROR",
                Category = "Financial",
                Severity = FindingSeverity.Critical,
                Title = "Inconsistencia en el total general de la factura",
                Description = $"Subtotal (${subtotal:F2}) + IVA (${taxAmount:F2}) = ${expectedInvoiceTotal:F2}, pero el total facturado indica ${totalAmount:F2}.",
                ExpectedValue = $"${expectedInvoiceTotal:F2}",
                ActualValue = $"${totalAmount:F2}",
                FinancialImpactUsd = totalMathDiff
            });
        }

        // 5. Conciliación cruzada contra Orden de Compra (3-Way / 2-Way Match)
        if (matchedPo is null)
        {
            findings.Add(new AuditFinding
            {
                AuditDocumentId = documentId,
                Code = "PURCHASE_ORDER_NOT_FOUND",
                Category = "Financial",
                Severity = FindingSeverity.Critical,
                Title = "Factura sin Orden de Compra autorizada en el ERP",
                Description = string.IsNullOrWhiteSpace(extracted.PurchaseOrderNumber)
                    ? "La factura no referencia ningún número de Orden de Compra."
                    : $"La Orden de Compra '{extracted.PurchaseOrderNumber}' no existe o no está aprobada en la base de datos.",
                ExpectedValue = "Orden de Compra registrada (ej. OC-2026-XXX)",
                ActualValue = extracted.PurchaseOrderNumber ?? "Sin referencia",
                FinancialImpactUsd = totalAmount
            });
            totalVarianceUsd = Math.Max(totalVarianceUsd, totalAmount);
        }
        else
        {
            // 5.A Validar RUC contra proveedor adjudicado en la OC
            var normalizedExtractedRuc = EcuadorTaxRules.NormalizeRuc(extracted.SupplierRuc);
            var normalizedPoRuc = EcuadorTaxRules.NormalizeRuc(matchedPo.SupplierRuc);
            if (!string.IsNullOrEmpty(normalizedExtractedRuc) && normalizedExtractedRuc != normalizedPoRuc)
            {
                findings.Add(new AuditFinding
                {
                    AuditDocumentId = documentId,
                    Code = "SUPPLIER_RUC_MISMATCH",
                    Category = "SupplierIdentity",
                    Severity = FindingSeverity.Critical,
                    Title = "RUC de factura no coincide con el proveedor adjudicado",
                    Description = $"La Orden de Compra {matchedPo.OrderNumber} fue emitida a '{matchedPo.SupplierName}' (RUC {matchedPo.SupplierRuc}), pero la factura proviene del RUC {extracted.SupplierRuc}.",
                    ExpectedValue = $"{matchedPo.SupplierRuc} ({matchedPo.SupplierName})",
                    ActualValue = $"{extracted.SupplierRuc} ({extracted.SupplierName ?? "Desconocido"})",
                    FinancialImpactUsd = totalAmount
                });
            }

            // 5.B Cruce de Subtotal (detección de sobreprecio o subfacturación)
            var subtotalDiff = Math.Round(subtotal - matchedPo.Subtotal, 2, MidpointRounding.AwayFromZero);
            if (subtotalDiff > EcuadorTaxRules.RoundingToleranceUsd)
            {
                totalVarianceUsd += subtotalDiff;
                findings.Add(new AuditFinding
                {
                    AuditDocumentId = documentId,
                    Code = "OVERPRICE_DETECTED",
                    Category = "Financial",
                    Severity = FindingSeverity.Critical,
                    Title = "Sobreprecio detectado respecto a Orden de Compra",
                    Description = $"El subtotal facturado (${subtotal:F2}) excede en ${subtotalDiff:F2} el monto pactado en la {matchedPo.OrderNumber} (${matchedPo.Subtotal:F2}).",
                    ExpectedValue = $"${matchedPo.Subtotal:F2}",
                    ActualValue = $"${subtotal:F2}",
                    FinancialImpactUsd = subtotalDiff
                });
            }
            else if (subtotalDiff < -EcuadorTaxRules.RoundingToleranceUsd)
            {
                var absDiff = Math.Abs(subtotalDiff);
                findings.Add(new AuditFinding
                {
                    AuditDocumentId = documentId,
                    Code = "PARTIAL_OR_UNDER_BILLING",
                    Category = "Financial",
                    Severity = FindingSeverity.Warning,
                    Title = "Facturación inferior al monto de la Orden de Compra",
                    Description = $"El subtotal facturado (${subtotal:F2}) es menor por ${absDiff:F2} respecto a la {matchedPo.OrderNumber} (${matchedPo.Subtotal:F2}). Verificar si corresponde a entrega parcial.",
                    ExpectedValue = $"${matchedPo.Subtotal:F2}",
                    ActualValue = $"${subtotal:F2}",
                    FinancialImpactUsd = absDiff
                });
            }

            // 5.C Cruce detallado de precios unitarios por ítem cuando aplique
            foreach (var poItem in matchedPo.Items)
            {
                var matchingLine = extracted.LineItems.FirstOrDefault(li =>
                    li.Description.Contains(poItem.Sku, StringComparison.OrdinalIgnoreCase) ||
                    li.Description.Contains(poItem.Description.Split(' ')[0], StringComparison.OrdinalIgnoreCase));

                if (matchingLine is not null)
                {
                    var unitDiff = Math.Round(matchingLine.UnitPrice - poItem.UnitPrice, 2, MidpointRounding.AwayFromZero);
                    if (unitDiff > EcuadorTaxRules.RoundingToleranceUsd)
                    {
                        var lineOvercharge = Math.Round(unitDiff * matchingLine.Quantity, 2, MidpointRounding.AwayFromZero);
                        findings.Add(new AuditFinding
                        {
                            AuditDocumentId = documentId,
                            Code = "ITEM_UNIT_OVERPRICE",
                            Category = "Financial",
                            Severity = FindingSeverity.Warning,
                            Title = $"Tarifa unitaria excedida en ítem ({poItem.Sku})",
                            Description = $"El ítem '{matchingLine.Description}' se facturó a ${matchingLine.UnitPrice:F2}/u cuando el precio acordado en OC es ${poItem.UnitPrice:F2}/u.",
                            ExpectedValue = $"${poItem.UnitPrice:F2} x {poItem.Quantity}",
                            ActualValue = $"${matchingLine.UnitPrice:F2} x {matchingLine.Quantity}",
                            FinancialImpactUsd = lineOvercharge
                        });
                    }
                }
            }
        }

        // 6. Resultado final de auditoría de factura
        var hasCritical = findings.Any(f => f.Severity == FindingSeverity.Critical);
        var hasWarning = findings.Any(f => f.Severity == FindingSeverity.Warning);

        if (!hasCritical && !hasWarning && matchedPo is not null)
        {
            findings.Add(new AuditFinding
            {
                AuditDocumentId = documentId,
                Code = "PERFECT_PO_RECONCILIATION",
                Category = "Financial",
                Severity = FindingSeverity.Info,
                Title = "Conciliación 100% conforme con Orden de Compra e IVA 15%",
                Description = $"RUC ({matchedPo.SupplierRuc}), subtotal (${subtotal:F2}), IVA 15% (${taxAmount:F2}) y total (${totalAmount:F2}) cuadran exactamente con la {matchedPo.OrderNumber}.",
                ExpectedValue = $"${matchedPo.TotalAmount:F2}",
                ActualValue = $"${totalAmount:F2}",
                FinancialImpactUsd = 0m
            });

            return new ReconciliationOutcome(
                AuditStatus.ApprovedAuto,
                RiskLevel.Low,
                0m,
                findings);
        }

        var risk = hasCritical
            ? (totalVarianceUsd >= 500m ? RiskLevel.Critical : RiskLevel.High)
            : RiskLevel.Medium;

        return new ReconciliationOutcome(
            AuditStatus.FlaggedDiscrepancy,
            risk,
            Math.Round(totalVarianceUsd, 2, MidpointRounding.AwayFromZero),
            findings);
    }

    public ReconciliationOutcome EvaluateContract(
        Guid documentId,
        ExtractedDocumentPayload extracted)
    {
        var findings = new List<AuditFinding>();
        decimal totalExposureUsd = 0m;

        if (!string.IsNullOrWhiteSpace(extracted.SupplierRuc) && !EcuadorTaxRules.IsValidRuc(extracted.SupplierRuc, out var rucReason))
        {
            findings.Add(new AuditFinding
            {
                AuditDocumentId = documentId,
                Code = "CONTRACT_SUPPLIER_RUC_INVALID",
                Category = "SupplierIdentity",
                Severity = FindingSeverity.Warning,
                Title = "RUC de contraparte contractual presenta formato inválido",
                Description = rucReason,
                ExpectedValue = "13 dígitos válidos",
                ActualValue = extracted.SupplierRuc
            });
        }

        foreach (var clause in extracted.RiskClauses)
        {
            var severity = clause.Severity.ToUpperInvariant() switch
            {
                "CRITICAL" or "HIGH" => FindingSeverity.Critical,
                "MEDIUM" => FindingSeverity.Warning,
                _ => FindingSeverity.Info
            };

            if (clause.EstimatedFinancialExposureUsd is > 0m)
            {
                totalExposureUsd += clause.EstimatedFinancialExposureUsd.Value;
            }

            findings.Add(new AuditFinding
            {
                AuditDocumentId = documentId,
                Code = $"CONTRACT_{clause.RiskCategory.ToUpperInvariant().Replace(' ', '_')}",
                Category = "ContractualRisk",
                Severity = severity,
                Title = $"Cláusula de riesgo detectada ({clause.ClauseReference})",
                Description = clause.Explanation,
                ExpectedValue = "Cláusula equilibrada conforme a política legal corporativa",
                ActualValue = $"{clause.RiskCategory} ({clause.Severity})",
                FinancialImpactUsd = clause.EstimatedFinancialExposureUsd
            });
        }

        var hasCritical = findings.Any(f => f.Severity == FindingSeverity.Critical);
        var hasWarning = findings.Any(f => f.Severity == FindingSeverity.Warning);

        if (!hasCritical && !hasWarning)
        {
            if (findings.Count == 0)
            {
                findings.Add(new AuditFinding
                {
                    AuditDocumentId = documentId,
                    Code = "CONTRACT_COMPLIANT",
                    Category = "ContractualRisk",
                    Severity = FindingSeverity.Info,
                    Title = "Contrato sin cláusulas abusivas detectadas",
                    Description = "El análisis legal automatizado no identificó penalidades desproporcionadas ni renovaciones forzosas.",
                    FinancialImpactUsd = 0m
                });
            }

            return new ReconciliationOutcome(
                AuditStatus.ApprovedAuto,
                RiskLevel.Low,
                0m,
                findings);
        }

        var overallRisk = (extracted.OverallContractRisk ?? string.Empty).ToUpperInvariant() switch
        {
            "CRITICAL" => RiskLevel.Critical,
            "HIGH" => RiskLevel.High,
            "MEDIUM" => RiskLevel.Medium,
            _ => hasCritical ? RiskLevel.High : RiskLevel.Medium
        };

        return new ReconciliationOutcome(
            AuditStatus.FlaggedDiscrepancy,
            overallRisk,
            Math.Round(totalExposureUsd, 2, MidpointRounding.AwayFromZero),
            findings);
    }
}
