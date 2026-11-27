using System.Diagnostics;
using AuditAI.Application.Common.Interfaces;
using AuditAI.Application.Services;
using AuditAI.Domain.Entities;
using AuditAI.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuditAI.Infrastructure.Background;

public sealed class AuditBackgroundWorker(
    IAuditQueue auditQueue,
    IServiceScopeFactory scopeFactory,
    ReconciliationEngine reconciliationEngine,
    IAuditRealtimeNotifier realtimeNotifier,
    ILogger<AuditBackgroundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AuditBackgroundWorker iniciado y escuchando cola asíncrona Channel<Guid>.");

        while (!stoppingToken.IsCancellationRequested)
        {
            Guid documentId;
            try
            {
                documentId = await auditQueue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await ProcessSingleDocumentAsync(documentId, stoppingToken);
        }
    }

    private async Task ProcessSingleDocumentAsync(Guid documentId, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        using var scope = scopeFactory.CreateScope();

        var docRepo = scope.ServiceProvider.GetRequiredService<IAuditDocumentRepository>();
        var poRepo = scope.ServiceProvider.GetRequiredService<IPurchaseOrderRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var pdfExtractor = scope.ServiceProvider.GetRequiredService<IPdfTextExtractor>();
        var aiAnalyzer = scope.ServiceProvider.GetRequiredService<IAiDocumentAnalyzer>();

        var doc = await docRepo.GetByIdWithDetailsAsync(documentId, ct);
        if (doc is null)
        {
            logger.LogWarning("Documento {DocumentId} no encontrado al desencolar.", documentId);
            return;
        }

        try
        {
            // Etapa 1: Extracción nativa de PDF con PdfPig
            doc.Status = AuditStatus.ExtractingText;
            await unitOfWork.SaveChangesAsync(ct);
            await realtimeNotifier.NotifyDocumentUpdatedAsync(doc, "Extrayendo texto estructurado del PDF con PdfPig...", ct);

            var extractedPdf = await pdfExtractor.ExtractTextAsync(doc.StoredFilePath, ct);
            doc.ExtractedRawText = extractedPdf.Text;
            doc.PageCount = extractedPdf.PageCount;

            // Etapa 2: Extracción tipada con Gemini Structured Outputs
            doc.Status = AuditStatus.AnalyzingWithAi;
            await unitOfWork.SaveChangesAsync(ct);
            await realtimeNotifier.NotifyDocumentUpdatedAsync(doc, "Ejecutando inferencia estructurada con Gemini IA...", ct);

            var aiResult = await aiAnalyzer.AnalyzeDocumentAsync(doc.OriginalFileName, extractedPdf.Text, ct);
            var payload = aiResult.Payload;

            doc.DocumentType = string.Equals(payload.DocumentKind, "CONTRACT", StringComparison.OrdinalIgnoreCase)
                ? DocumentType.Contract
                : DocumentType.Invoice;

            doc.ExtractedInvoiceNumber = payload.InvoiceNumber;
            doc.ExtractedOrderNumber = payload.PurchaseOrderNumber;
            doc.ExtractedSupplierName = payload.SupplierName;
            doc.ExtractedSupplierRuc = payload.SupplierRuc;
            doc.ExtractedSubtotal = payload.Subtotal;
            doc.ExtractedTaxRate = payload.TaxRatePercent.HasValue ? payload.TaxRatePercent.Value / 100m : null;
            doc.ExtractedTaxAmount = payload.TaxAmount;
            doc.ExtractedTotalAmount = payload.TotalAmount;
            doc.StructuredExtractionJson = aiResult.RawJson;
            doc.AiExecutiveSummary = payload.ExecutiveSummary;
            doc.AiModelUsed = aiResult.ModelUsed;

            // Etapa 3: Conciliación Financiera / Evaluación Legal
            doc.Status = AuditStatus.Reconciling;
            await unitOfWork.SaveChangesAsync(ct);
            await realtimeNotifier.NotifyDocumentUpdatedAsync(doc, "Cruzando datos contra órdenes de compra y reglas fiscales...", ct);

            ReconciliationOutcome outcome;
            if (doc.DocumentType == DocumentType.Contract)
            {
                outcome = reconciliationEngine.EvaluateContract(doc.Id, payload);
            }
            else
            {
                PurchaseOrder? matchedPo = null;
                if (!string.IsNullOrWhiteSpace(payload.PurchaseOrderNumber))
                {
                    matchedPo = await poRepo.GetByOrderNumberAsync(payload.PurchaseOrderNumber, ct);
                    if (matchedPo is not null)
                    {
                        doc.MatchedPurchaseOrderId = matchedPo.Id;
                        doc.MatchedPurchaseOrder = matchedPo;
                    }
                }

                outcome = reconciliationEngine.ReconcileInvoice(doc.Id, payload, matchedPo);
            }

            stopwatch.Stop();
            doc.Status = outcome.FinalStatus;
            doc.RiskLevel = outcome.RiskLevel;
            doc.DetectedVarianceAmount = outcome.TotalVarianceUsd;
            doc.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;
            doc.ProcessedAtUtc = DateTime.UtcNow;

            await docRepo.ReplaceFindingsAsync(doc.Id, outcome.Findings, ct);
            doc.Findings = outcome.Findings.ToList();
            await unitOfWork.SaveChangesAsync(ct);

            var finalMessage = doc.Status == AuditStatus.ApprovedAuto
                ? $"Auditoría aprobada automáticamente en {doc.ProcessingTimeMs} ms sin discrepancias."
                : $"Auditoría completada con alertas ({outcome.Findings.Count} hallazgos, riesgo {doc.RiskLevel}).";

            await realtimeNotifier.NotifyDocumentUpdatedAsync(doc, finalMessage, ct);
            logger.LogInformation(
                "Documento {FileName} ({DocumentId}) auditado en {ElapsedMs}ms -> Estado={Status}, Riesgo={Risk}",
                doc.OriginalFileName, doc.Id, doc.ProcessingTimeMs, doc.Status, doc.RiskLevel);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Error auditando el documento {DocumentId} ({FileName}).", doc.Id, doc.OriginalFileName);

            doc.Status = AuditStatus.Failed;
            doc.RiskLevel = RiskLevel.High;
            doc.ProcessingTimeMs = (int)stopwatch.ElapsedMilliseconds;
            doc.ProcessedAtUtc = DateTime.UtcNow;
            doc.AiExecutiveSummary = $"Error durante el procesamiento automatizado: {ex.Message}";

            var errorFinding = new AuditFinding
            {
                AuditDocumentId = doc.Id,
                Code = "PROCESSING_PIPELINE_ERROR",
                Category = "System",
                Severity = FindingSeverity.Critical,
                Title = "Fallo en pipeline de extracción o inferencia",
                Description = ex.Message
            };

            await docRepo.ReplaceFindingsAsync(doc.Id, [errorFinding], ct);
            doc.Findings = [errorFinding];
            await unitOfWork.SaveChangesAsync(ct);
            await realtimeNotifier.NotifyDocumentUpdatedAsync(doc, "Error al procesar el documento.", ct);
        }
    }
}
