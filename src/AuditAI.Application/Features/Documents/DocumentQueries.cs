using AuditAI.Application.Common.Cqrs;
using AuditAI.Application.Common.Interfaces;
using AuditAI.Application.DTOs;
using AuditAI.Domain.Enums;

namespace AuditAI.Application.Features.Documents;

public sealed record GetAuditDocumentsQuery : IQuery<IReadOnlyList<AuditDocumentSummaryDto>>;

public sealed class GetAuditDocumentsQueryHandler(IAuditDocumentRepository documentRepository)
    : IQueryHandler<GetAuditDocumentsQuery, IReadOnlyList<AuditDocumentSummaryDto>>
{
    public async Task<IReadOnlyList<AuditDocumentSummaryDto>> HandleAsync(GetAuditDocumentsQuery query, CancellationToken ct = default)
    {
        var docs = await documentRepository.GetAllWithFindingsAsync(ct);
        return docs.Select(d => d.ToSummaryDto()).ToList();
    }
}

public sealed record GetAuditDocumentByIdQuery(Guid Id) : IQuery<AuditDocumentDetailDto?>;

public sealed class GetAuditDocumentByIdQueryHandler(IAuditDocumentRepository documentRepository)
    : IQueryHandler<GetAuditDocumentByIdQuery, AuditDocumentDetailDto?>
{
    public async Task<AuditDocumentDetailDto?> HandleAsync(GetAuditDocumentByIdQuery query, CancellationToken ct = default)
    {
        var doc = await documentRepository.GetByIdWithDetailsAsync(query.Id, ct);
        return doc?.ToDetailDto();
    }
}

public sealed record GetDashboardMetricsQuery : IQuery<DashboardMetricsDto>;

public sealed class GetDashboardMetricsQueryHandler(IAuditDocumentRepository documentRepository)
    : IQueryHandler<GetDashboardMetricsQuery, DashboardMetricsDto>
{
    public async Task<DashboardMetricsDto> HandleAsync(GetDashboardMetricsQuery query, CancellationToken ct = default)
    {
        var docs = await documentRepository.GetAllWithFindingsAsync(ct);

        var inProgress = docs.Count(d => d.Status is AuditStatus.Queued or AuditStatus.ExtractingText or AuditStatus.AnalyzingWithAi or AuditStatus.Reconciling);
        var approved = docs.Count(d => d.Status is AuditStatus.ApprovedAuto or AuditStatus.ManuallyApproved);
        var flagged = docs.Count(d => d.Status == AuditStatus.FlaggedDiscrepancy);
        var rejected = docs.Count(d => d.Status == AuditStatus.Rejected);
        var highRisk = docs.Count(d => d.RiskLevel is RiskLevel.High or RiskLevel.Critical);

        var totalAuditedAmount = docs
            .Where(d => d.ExtractedTotalAmount.HasValue)
            .Sum(d => d.ExtractedTotalAmount!.Value);

        var totalVariance = docs
            .Where(d => d.DetectedVarianceAmount.HasValue)
            .Sum(d => d.DetectedVarianceAmount!.Value);

        var processedTimes = docs
            .Where(d => d.ProcessingTimeMs.HasValue)
            .Select(d => (double)d.ProcessingTimeMs!.Value)
            .ToList();

        var avgMs = processedTimes.Count > 0 ? Math.Round(processedTimes.Average(), 0) : 0;

        return new DashboardMetricsDto(
            docs.Count,
            inProgress,
            approved,
            flagged,
            rejected,
            highRisk,
            Math.Round(totalAuditedAmount, 2),
            Math.Round(totalVariance, 2),
            avgMs);
    }
}

public sealed record GetPurchaseOrdersQuery : IQuery<IReadOnlyList<PurchaseOrderDto>>;

public sealed class GetPurchaseOrdersQueryHandler(IPurchaseOrderRepository purchaseOrderRepository)
    : IQueryHandler<GetPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderDto>>
{
    public async Task<IReadOnlyList<PurchaseOrderDto>> HandleAsync(GetPurchaseOrdersQuery query, CancellationToken ct = default)
    {
        var orders = await purchaseOrderRepository.GetAllWithItemsAsync(ct);
        return orders.Select(o => o.ToDto()).ToList();
    }
}
