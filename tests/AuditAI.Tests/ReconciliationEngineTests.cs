using AuditAI.Application.DTOs;
using AuditAI.Application.Services;
using AuditAI.Domain.Entities;
using AuditAI.Domain.Enums;

namespace AuditAI.Tests;

public sealed class ReconciliationEngineTests
{
    private readonly ReconciliationEngine _engine = new();

    private static PurchaseOrder CreateSamplePurchaseOrder() => new()
    {
        OrderNumber = "OC-2026-001",
        SupplierName = "TecnoAndina Soluciones S.A.",
        SupplierRuc = "0992847561001",
        Subtotal = 11000.00m,
        TaxRate = 0.15m,
        TaxAmount = 1650.00m,
        TotalAmount = 12650.00m,
        Items =
        [
            new PurchaseOrderItem
            {
                Sku = "SRV-DELL-R650",
                Description = "Servidor Rack Dell PowerEdge R650",
                Quantity = 2,
                UnitPrice = 4200.00m,
                LineTotal = 8400.00m
            },
            new PurchaseOrderItem
            {
                Sku = "SW-FORTI-200F",
                Description = "Firewall FortiGate 200F",
                Quantity = 2,
                UnitPrice = 1300.00m,
                LineTotal = 2600.00m
            }
        ]
    };

    [Fact]
    public void ReconcileInvoice_ShouldApproveAutomatically_WhenInvoiceMatchesPurchaseOrderExactly()
    {
        var po = CreateSamplePurchaseOrder();
        var payload = new ExtractedDocumentPayload(
            DocumentKind: "INVOICE",
            InvoiceNumber: "001-002-000045821",
            PurchaseOrderNumber: "OC-2026-001",
            SupplierName: "TecnoAndina Soluciones S.A.",
            SupplierRuc: "0992847561001",
            IssueDate: "2026-11-25",
            Subtotal: 11000.00m,
            TaxRatePercent: 15m,
            TaxAmount: 1650.00m,
            TotalAmount: 12650.00m,
            LineItems:
            [
                new ExtractedLineItemPayload("SRV-DELL-R650 Servidor Rack", 2, 4200.00m, 8400.00m),
                new ExtractedLineItemPayload("SW-FORTI-200F Firewall", 2, 1300.00m, 2600.00m)
            ],
            RiskClauses: [],
            OverallContractRisk: "LOW",
            ExecutiveSummary: "Factura conforme.");

        var outcome = _engine.ReconcileInvoice(Guid.NewGuid(), payload, po);

        Assert.Equal(AuditStatus.ApprovedAuto, outcome.FinalStatus);
        Assert.Equal(RiskLevel.Low, outcome.RiskLevel);
        Assert.Equal(0m, outcome.TotalVarianceUsd);
        Assert.Contains(outcome.Findings, f => f.Code == "PERFECT_PO_RECONCILIATION");
    }

    [Fact]
    public void ReconcileInvoice_ShouldDetectOverpriceAndItemVariance_WhenBilledAbovePo()
    {
        var po = new PurchaseOrder
        {
            OrderNumber = "OC-2026-002",
            SupplierName = "CloudLatam Cia. Ltda.",
            SupplierRuc = "1792458312001",
            Subtotal = 4000.00m,
            TaxRate = 0.15m,
            TaxAmount = 600.00m,
            TotalAmount = 4600.00m,
            Items =
            [
                new PurchaseOrderItem { Sku = "LIC-M365-E5", Description = "Licencias Seguridad", Quantity = 50, UnitPrice = 48.00m, LineTotal = 2400.00m },
                new PurchaseOrderItem { Sku = "SEC-SOC-MON", Description = "Monitoreo SOC", Quantity = 1, UnitPrice = 1600.00m, LineTotal = 1600.00m }
            ]
        };

        var payload = new ExtractedDocumentPayload(
            DocumentKind: "INVOICE",
            InvoiceNumber: "002-001-000019304",
            PurchaseOrderNumber: "OC-2026-002",
            SupplierName: "CloudLatam Cia. Ltda.",
            SupplierRuc: "1792458312001",
            IssueDate: "2026-11-26",
            Subtotal: 4500.00m,
            TaxRatePercent: 15m,
            TaxAmount: 675.00m,
            TotalAmount: 5175.00m,
            LineItems:
            [
                new ExtractedLineItemPayload("LIC-M365-E5 Licencias Seguridad", 50, 58.00m, 2900.00m),
                new ExtractedLineItemPayload("SEC-SOC-MON Monitoreo SOC", 1, 1600.00m, 1600.00m)
            ],
            RiskClauses: [],
            OverallContractRisk: "LOW",
            ExecutiveSummary: "Sobreprecio de $500.");

        var outcome = _engine.ReconcileInvoice(Guid.NewGuid(), payload, po);

        Assert.Equal(AuditStatus.FlaggedDiscrepancy, outcome.FinalStatus);
        Assert.Equal(RiskLevel.Critical, outcome.RiskLevel);
        Assert.Equal(500.00m, outcome.TotalVarianceUsd);
        Assert.Contains(outcome.Findings, f => f.Code == "OVERPRICE_DETECTED" && f.FinancialImpactUsd == 500.00m);
        Assert.Contains(outcome.Findings, f => f.Code == "ITEM_UNIT_OVERPRICE");
    }

    [Fact]
    public void ReconcileInvoice_ShouldDetectIvaCalculationError_WhenTaxDiffersFrom15Percent()
    {
        var po = new PurchaseOrder
        {
            OrderNumber = "OC-2026-003",
            SupplierName = "Logística Pacífico S.A.",
            SupplierRuc = "0993124578001",
            Subtotal = 3800.00m,
            TaxRate = 0.15m,
            TaxAmount = 570.00m,
            TotalAmount = 4370.00m
        };

        var payload = new ExtractedDocumentPayload(
            DocumentKind: "INVOICE",
            InvoiceNumber: "001-004-000088120",
            PurchaseOrderNumber: "OC-2026-003",
            SupplierName: "Logística Pacífico S.A.",
            SupplierRuc: "0993124578001",
            IssueDate: "2026-11-26",
            Subtotal: 3800.00m,
            TaxRatePercent: 20m,
            TaxAmount: 760.00m,
            TotalAmount: 4560.00m,
            LineItems: [],
            RiskClauses: [],
            OverallContractRisk: "LOW",
            ExecutiveSummary: "IVA cobrado al 20%.");

        var outcome = _engine.ReconcileInvoice(Guid.NewGuid(), payload, po);

        Assert.Equal(AuditStatus.FlaggedDiscrepancy, outcome.FinalStatus);
        Assert.Equal(190.00m, outcome.TotalVarianceUsd);
        Assert.Contains(outcome.Findings, f => f.Code == "IVA_CALCULATION_ERROR" && f.FinancialImpactUsd == 190.00m);
    }

    [Fact]
    public void ReconcileInvoice_ShouldFlagInvalidRucAndMissingPurchaseOrder()
    {
        var payload = new ExtractedDocumentPayload(
            DocumentKind: "INVOICE",
            InvoiceNumber: "003-001-000000991",
            PurchaseOrderNumber: "OC-2026-999",
            SupplierName: "Proveedor Fantasma",
            SupplierRuc: "9912345670000",
            IssueDate: "2026-11-27",
            Subtotal: 6200.00m,
            TaxRatePercent: 15m,
            TaxAmount: 930.00m,
            TotalAmount: 7130.00m,
            LineItems: [],
            RiskClauses: [],
            OverallContractRisk: "LOW",
            ExecutiveSummary: "Factura sospechosa.");

        var outcome = _engine.ReconcileInvoice(Guid.NewGuid(), payload, matchedPo: null);

        Assert.Equal(AuditStatus.FlaggedDiscrepancy, outcome.FinalStatus);
        Assert.Equal(RiskLevel.Critical, outcome.RiskLevel);
        Assert.Contains(outcome.Findings, f => f.Code == "RUC_INVALID_FORMAT");
        Assert.Contains(outcome.Findings, f => f.Code == "PURCHASE_ORDER_NOT_FOUND");
    }

    [Fact]
    public void EvaluateContract_ShouldFlagCriticalRisk_WhenAbusiveClausesPresent()
    {
        var payload = new ExtractedDocumentPayload(
            DocumentKind: "CONTRACT",
            InvoiceNumber: null,
            PurchaseOrderNumber: null,
            SupplierName: "DataCore Outsourcing S.A.",
            SupplierRuc: "1793019284001",
            IssueDate: "2026-11-26",
            Subtotal: null,
            TaxRatePercent: null,
            TaxAmount: null,
            TotalAmount: 48000.00m,
            LineItems: [],
            RiskClauses:
            [
                new ExtractedContractClausePayload("Cláusula 7", "Penalidad Unilateral", "CRITICAL", "Multa unilateral de $25,000 USD al cliente.", 25000.00m),
                new ExtractedContractClausePayload("Cláusula 4", "Renovación Forzosa", "HIGH", "Renovación automática por 36 meses con incremento del 18%.", 15000.00m)
            ],
            OverallContractRisk: "CRITICAL",
            ExecutiveSummary: "Contrato altamente riesgoso.");

        var outcome = _engine.EvaluateContract(Guid.NewGuid(), payload);

        Assert.Equal(AuditStatus.FlaggedDiscrepancy, outcome.FinalStatus);
        Assert.Equal(RiskLevel.Critical, outcome.RiskLevel);
        Assert.Equal(40000.00m, outcome.TotalVarianceUsd);
        Assert.Equal(2, outcome.Findings.Count);
    }
}
