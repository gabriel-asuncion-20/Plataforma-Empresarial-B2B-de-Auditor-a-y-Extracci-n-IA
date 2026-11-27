using AuditAI.Application.Common.Cqrs;
using AuditAI.Application.DTOs;
using AuditAI.Application.Features.Auth;
using AuditAI.Application.Features.Documents;
using AuditAI.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AuditAI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICqrsDispatcher, CqrsDispatcher>();
        services.AddSingleton<ReconciliationEngine>();

        // Commands
        services.AddScoped<ICommandHandler<LoginCommand, AuthResponseDto>, LoginCommandHandler>();
        services.AddScoped<ICommandHandler<DemoLoginCommand, AuthResponseDto>, DemoLoginCommandHandler>();
        services.AddScoped<ICommandHandler<UploadAuditDocumentCommand, AuditDocumentSummaryDto>, UploadAuditDocumentCommandHandler>();
        services.AddScoped<ICommandHandler<EnqueueSamplePdfCommand, AuditDocumentSummaryDto>, EnqueueSamplePdfCommandHandler>();
        services.AddScoped<ICommandHandler<ReviewAuditDocumentCommand, AuditDocumentDetailDto>, ReviewAuditDocumentCommandHandler>();

        // Queries
        services.AddScoped<IQueryHandler<GetAuditDocumentsQuery, IReadOnlyList<AuditDocumentSummaryDto>>, GetAuditDocumentsQueryHandler>();
        services.AddScoped<IQueryHandler<GetAuditDocumentByIdQuery, AuditDocumentDetailDto?>, GetAuditDocumentByIdQueryHandler>();
        services.AddScoped<IQueryHandler<GetDashboardMetricsQuery, DashboardMetricsDto>, GetDashboardMetricsQueryHandler>();
        services.AddScoped<IQueryHandler<GetPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderDto>>, GetPurchaseOrdersQueryHandler>();

        return services;
    }
}
