using AuditAI.Domain.Enums;

namespace AuditAI.Domain.Entities;

public class AuditDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int PageCount { get; set; }
    public DocumentType DocumentType { get; set; } = DocumentType.Invoice;
    public AuditStatus Status { get; set; } = AuditStatus.Queued;
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;

    public Guid? MatchedPurchaseOrderId { get; set; }
    public PurchaseOrder? MatchedPurchaseOrder { get; set; }

    public string? ExtractedOrderNumber { get; set; }
    public string? ExtractedInvoiceNumber { get; set; }
    public string? ExtractedSupplierName { get; set; }
    public string? ExtractedSupplierRuc { get; set; }
    public decimal? ExtractedSubtotal { get; set; }
    public decimal? ExtractedTaxRate { get; set; }
    public decimal? ExtractedTaxAmount { get; set; }
    public decimal? ExtractedTotalAmount { get; set; }
    public decimal? DetectedVarianceAmount { get; set; }

    public string? ExtractedRawText { get; set; }
    public string? StructuredExtractionJson { get; set; }
    public string? AiExecutiveSummary { get; set; }
    public string? AiModelUsed { get; set; }
    public int? ProcessingTimeMs { get; set; }

    public Guid UploadedByUserId { get; set; }
    public string UploadedByName { get; set; } = string.Empty;

    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }

    public ICollection<AuditFinding> Findings { get; set; } = new List<AuditFinding>();

    public void ApproveManually(Guid reviewerId, string reviewerName, string? notes)
    {
        if (Status is AuditStatus.Queued or AuditStatus.ExtractingText or AuditStatus.AnalyzingWithAi or AuditStatus.Reconciling)
        {
            throw new InvalidOperationException("No se puede aprobar un documento que aún está en procesamiento.");
        }

        Status = AuditStatus.ManuallyApproved;
        ReviewedByUserId = reviewerId;
        ReviewedByName = reviewerName;
        ReviewNotes = string.IsNullOrWhiteSpace(notes)
            ? "Aprobado manualmente por supervisión ejecutiva."
            : notes.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
    }

    public void Reject(Guid reviewerId, string reviewerName, string? reason)
    {
        if (Status is AuditStatus.Queued or AuditStatus.ExtractingText or AuditStatus.AnalyzingWithAi or AuditStatus.Reconciling)
        {
            throw new InvalidOperationException("No se puede rechazar un documento que aún está en procesamiento.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Se requiere un motivo de auditoría para rechazar el documento.", nameof(reason));
        }

        Status = AuditStatus.Rejected;
        ReviewedByUserId = reviewerId;
        ReviewedByName = reviewerName;
        ReviewNotes = reason.Trim();
        ReviewedAtUtc = DateTime.UtcNow;
    }
}

public class AuditFinding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AuditDocumentId { get; set; }
    public AuditDocument? AuditDocument { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public FindingSeverity Severity { get; set; } = FindingSeverity.Info;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ExpectedValue { get; set; }
    public string? ActualValue { get; set; }
    public decimal? FinancialImpactUsd { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
