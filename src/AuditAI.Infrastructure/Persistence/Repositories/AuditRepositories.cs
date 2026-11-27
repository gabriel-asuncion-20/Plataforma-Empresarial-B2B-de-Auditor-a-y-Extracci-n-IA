using AuditAI.Application.Common.Interfaces;
using AuditAI.Domain.Entities;
using AuditAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AuditAI.Infrastructure.Persistence.Repositories;

public sealed class UserRepository(AuditDbContext db) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetFirstByRoleAsync(UserRole role, CancellationToken ct = default)
        => db.Users.Where(u => u.Role == role && u.IsActive).OrderBy(u => u.CreatedAtUtc).FirstOrDefaultAsync(ct);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
}

public sealed class PurchaseOrderRepository(AuditDbContext db) : IPurchaseOrderRepository
{
    public async Task<IReadOnlyList<PurchaseOrder>> GetAllWithItemsAsync(CancellationToken ct = default)
        => await db.PurchaseOrders
            .Include(p => p.Items)
            .OrderBy(p => p.OrderNumber)
            .ToListAsync(ct);

    public Task<PurchaseOrder?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
    {
        var normalized = orderNumber.Trim().ToUpperInvariant();
        return db.PurchaseOrders
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.OrderNumber.ToUpper() == normalized, ct);
    }
}

public sealed class AuditDocumentRepository(AuditDbContext db) : IAuditDocumentRepository
{
    public async Task AddAsync(AuditDocument document, CancellationToken ct = default)
        => await db.AuditDocuments.AddAsync(document, ct);

    public Task<AuditDocument?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default)
        => db.AuditDocuments
            .Include(d => d.Findings)
            .Include(d => d.MatchedPurchaseOrder)
                .ThenInclude(po => po!.Items)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<AuditDocument>> GetAllWithFindingsAsync(CancellationToken ct = default)
        => await db.AuditDocuments
            .Include(d => d.Findings)
            .OrderByDescending(d => d.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task ReplaceFindingsAsync(Guid documentId, IEnumerable<AuditFinding> findings, CancellationToken ct = default)
    {
        var existing = await db.AuditFindings.Where(f => f.AuditDocumentId == documentId).ToListAsync(ct);
        if (existing.Count > 0)
        {
            db.AuditFindings.RemoveRange(existing);
        }

        await db.AuditFindings.AddRangeAsync(findings, ct);
    }
}
