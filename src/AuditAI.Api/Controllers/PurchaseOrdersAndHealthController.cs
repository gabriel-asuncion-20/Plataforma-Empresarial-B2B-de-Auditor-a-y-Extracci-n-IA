using AuditAI.Application.Common.Cqrs;
using AuditAI.Application.DTOs;
using AuditAI.Application.Features.Documents;
using AuditAI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditAI.Api.Controllers;

[ApiController]
[Route("api/purchase-orders")]
[Authorize]
public sealed class PurchaseOrdersController(ICqrsDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PurchaseOrderDto>>> GetAll(CancellationToken ct)
    {
        var orders = await dispatcher.SendQueryAsync<GetPurchaseOrdersQuery, IReadOnlyList<PurchaseOrderDto>>(
            new GetPurchaseOrdersQuery(), ct);
        return Ok(orders);
    }
}

[ApiController]
public sealed class HealthController(AuditDbContext dbContext) : ControllerBase
{
    [HttpGet("/health")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckHealth(CancellationToken ct)
    {
        var dbConnected = await dbContext.Database.CanConnectAsync(ct);
        return Ok(new
        {
            status = dbConnected ? "Healthy" : "Degraded",
            service = "AuditAI.Enterprise.Api",
            database = dbConnected ? "Neon PostgreSQL Connected" : "Unreachable",
            schema = AuditDbContext.SchemaName,
            timestampUtc = DateTime.UtcNow
        });
    }
}
