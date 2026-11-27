using AuditAI.Domain.Entities;
using AuditAI.Domain.Enums;

namespace AuditAI.Application.DTOs;

// === AI Structured Output Contracts ===
public sealed record ExtractedLineItemPayload(
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record ExtractedContractClausePayload(
    string ClauseReference,
    string RiskCategory,
    string Severity,
    string Explanation,
    decimal? EstimatedFinancialExposureUsd);

public sealed record ExtractedDocumentPayload(
    string DocumentKind,
    string? InvoiceNumber,
    string? PurchaseOrderNumber,
    string? SupplierName,
    string? SupplierRuc,
    string? IssueDate,
    decimal? Subtotal,
    decimal? TaxRatePercent,
    decimal? TaxAmount,
    decimal? TotalAmount,
    IReadOnlyList<ExtractedLineItemPayload> LineItems,
    IReadOnlyList<ExtractedContractClausePayload> RiskClauses,
    string? OverallContractRisk,
    string ExecutiveSummary);

public sealed record DocumentAiAnalysisResult(
    ExtractedDocumentPayload Payload,
    string RawJson,
    string ModelUsed);

public sealed record ExtractedPdfResult(
    string Text,
    int PageCount);

// === Auth DTOs ===
public sealed record LoginRequestDto(string Email, string Password);

public sealed record DemoLoginRequestDto(string Role);

public sealed record UserDto(
    Guid Id,
    string Email,
    string FullName,
    string Role);

public sealed record AuthResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    UserDto User);

// === Purchase Order DTOs ===
public sealed record PurchaseOrderItemDto(
    Guid Id,
    string Sku,
    string Description,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public sealed record PurchaseOrderDto(
    Guid Id,
    string OrderNumber,
    string SupplierName,
    string SupplierRuc,
    string Currency,
    decimal Subtotal,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount,
    DateTime IssuedAtUtc,
    DateTime ExpectedDeliveryUtc,
    string Status,
    IReadOnlyList<PurchaseOrderItemDto> Items);

// === Audit Document & Findings DTOs ===
public sealed record AuditFindingDto(
    Guid Id,
    string Code,
    string Category,
    string Severity,
    string Title,
    string Description,
    string? ExpectedValue,
    string? ActualValue,
    decimal? FinancialImpactUsd);

public sealed record AuditDocumentSummaryDto(
    Guid Id,
    string OriginalFileName,
    long FileSizeBytes,
    int PageCount,
    string DocumentType,
    string Status,
    string RiskLevel,
    string? ExtractedOrderNumber,
    string? ExtractedInvoiceNumber,
    string? ExtractedSupplierName,
    string? ExtractedSupplierRuc,
    decimal? ExtractedSubtotal,
    decimal? ExtractedTaxAmount,
    decimal? ExtractedTotalAmount,
    decimal? DetectedVarianceAmount,
    string? AiExecutiveSummary,
    string? AiModelUsed,
    int? ProcessingTimeMs,
    string UploadedByName,
    string? ReviewedByName,
    string? ReviewNotes,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    DateTime? ReviewedAtUtc,
    int FindingsCount,
    int CriticalFindingsCount);

public sealed record AuditDocumentDetailDto(
    Guid Id,
    string OriginalFileName,
    long FileSizeBytes,
    int PageCount,
    string DocumentType,
    string Status,
    string RiskLevel,
    Guid? MatchedPurchaseOrderId,
    PurchaseOrderDto? MatchedPurchaseOrder,
    string? ExtractedOrderNumber,
    string? ExtractedInvoiceNumber,
    string? ExtractedSupplierName,
    string? ExtractedSupplierRuc,
    decimal? ExtractedSubtotal,
    decimal? ExtractedTaxRate,
    decimal? ExtractedTaxAmount,
    decimal? ExtractedTotalAmount,
    decimal? DetectedVarianceAmount,
    string? StructuredExtractionJson,
    string? ExtractedRawTextPreview,
    string? AiExecutiveSummary,
    string? AiModelUsed,
    int? ProcessingTimeMs,
    string UploadedByName,
    string? ReviewedByName,
    string? ReviewNotes,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    DateTime? ReviewedAtUtc,
    IReadOnlyList<AuditFindingDto> Findings);

public sealed record DashboardMetricsDto(
    int TotalDocuments,
    int InProgressCount,
    int ApprovedCount,
    int FlaggedCount,
    int RejectedCount,
    int HighRiskCount,
    decimal TotalAuditedAmountUsd,
    decimal TotalDetectedVarianceUsd,
    double AverageProcessingTimeMs);

public sealed record SamplePdfInfoDto(
    string FileName,
    string Title,
    string ScenarioDescription,
    string ExpectedOutcome,
    long FileSizeBytes);

public sealed record RealtimeAuditUpdateDto(
    Guid DocumentId,
    string FileName,
    string Status,
    string RiskLevel,
    string StageMessage,
    decimal? DetectedVarianceAmount,
    int FindingsCount,
    DateTime TimestampUtc,
    AuditDocumentSummaryDto Document);

public static class AuditDtoMapper
{
    public static PurchaseOrderDto ToDto(this PurchaseOrder po) => new(
        po.Id,
        po.OrderNumber,
        po.SupplierName,
        po.SupplierRuc,
        po.Currency,
        po.Subtotal,
        po.TaxRate,
        po.TaxAmount,
        po.TotalAmount,
        po.IssuedAtUtc,
        po.ExpectedDeliveryUtc,
        po.Status,
        po.Items.Select(i => new PurchaseOrderItemDto(i.Id, i.Sku, i.Description, i.Quantity, i.UnitPrice, i.LineTotal)).ToList());

    public static AuditFindingDto ToDto(this AuditFinding f) => new(
        f.Id,
        f.Code,
        f.Category,
        f.Severity.ToString(),
        f.Title,
        f.Description,
        f.ExpectedValue,
        f.ActualValue,
        f.FinancialImpactUsd);

    public static AuditDocumentSummaryDto ToSummaryDto(this AuditDocument d) => new(
        d.Id,
        d.OriginalFileName,
        d.FileSizeBytes,
        d.PageCount,
        d.DocumentType.ToString(),
        d.Status.ToString(),
        d.RiskLevel.ToString(),
        d.ExtractedOrderNumber,
        d.ExtractedInvoiceNumber,
        d.ExtractedSupplierName,
        d.ExtractedSupplierRuc,
        d.ExtractedSubtotal,
        d.ExtractedTaxAmount,
        d.ExtractedTotalAmount,
        d.DetectedVarianceAmount,
        d.AiExecutiveSummary,
        d.AiModelUsed,
        d.ProcessingTimeMs,
        d.UploadedByName,
        d.ReviewedByName,
        d.ReviewNotes,
        d.CreatedAtUtc,
        d.ProcessedAtUtc,
        d.ReviewedAtUtc,
        d.Findings.Count,
        d.Findings.Count(f => f.Severity == FindingSeverity.Critical));

    public static AuditDocumentDetailDto ToDetailDto(this AuditDocument d) => new(
        d.Id,
        d.OriginalFileName,
        d.FileSizeBytes,
        d.PageCount,
        d.DocumentType.ToString(),
        d.Status.ToString(),
        d.RiskLevel.ToString(),
        d.MatchedPurchaseOrderId,
        d.MatchedPurchaseOrder?.ToDto(),
        d.ExtractedOrderNumber,
        d.ExtractedInvoiceNumber,
        d.ExtractedSupplierName,
        d.ExtractedSupplierRuc,
        d.ExtractedSubtotal,
        d.ExtractedTaxRate,
        d.ExtractedTaxAmount,
        d.ExtractedTotalAmount,
        d.DetectedVarianceAmount,
        d.StructuredExtractionJson,
        d.ExtractedRawText is { Length: > 2000 } raw ? raw[..2000] + "..." : d.ExtractedRawText,
        d.AiExecutiveSummary,
        d.AiModelUsed,
        d.ProcessingTimeMs,
        d.UploadedByName,
        d.ReviewedByName,
        d.ReviewNotes,
        d.CreatedAtUtc,
        d.ProcessedAtUtc,
        d.ReviewedAtUtc,
        d.Findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.CreatedAtUtc)
            .Select(f => f.ToDto())
            .ToList());
}
