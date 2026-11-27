using AuditAI.Application.Common.Interfaces;
using AuditAI.Application.DTOs;
using AuditAI.Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace AuditAI.Api.Hubs;

public sealed class AuditHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync("HubConnected", new
        {
            connectionId = Context.ConnectionId,
            connectedAtUtc = DateTime.UtcNow
        });
        await base.OnConnectedAsync();
    }
}

public sealed class SignalRAuditNotifier(IHubContext<AuditHub> hubContext) : IAuditRealtimeNotifier
{
    public Task NotifyDocumentUpdatedAsync(AuditDocument document, string stageMessage, CancellationToken ct = default)
    {
        var summary = document.ToSummaryDto();
        var payload = new RealtimeAuditUpdateDto(
            document.Id,
            document.OriginalFileName,
            document.Status.ToString(),
            document.RiskLevel.ToString(),
            stageMessage,
            document.DetectedVarianceAmount,
            summary.FindingsCount,
            DateTime.UtcNow,
            summary);

        return hubContext.Clients.All.SendAsync("AuditUpdated", payload, ct);
    }
}
