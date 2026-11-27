using AuditAI.Application.Common.Interfaces;
using AuditAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuditAI.Infrastructure.Persistence;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string SchemaName = "audit_ai";

    public DbSet<User> Users => Set<User>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<AuditDocument> AuditDocuments => Set<AuditDocument>();
    public DbSet<AuditFinding> AuditFindings => Set<AuditFinding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SchemaName);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Email).HasMaxLength(180).IsRequired();
            entity.Property(u => u.FullName).HasMaxLength(180).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(u => u.Role).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("purchase_orders");
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.OrderNumber).IsUnique();
            entity.Property(p => p.OrderNumber).HasMaxLength(64).IsRequired();
            entity.Property(p => p.SupplierName).HasMaxLength(200).IsRequired();
            entity.Property(p => p.SupplierRuc).HasMaxLength(20).IsRequired();
            entity.Property(p => p.Currency).HasMaxLength(8).IsRequired();
            entity.Property(p => p.Status).HasMaxLength(32).IsRequired();
            entity.Property(p => p.Subtotal).HasPrecision(18, 2);
            entity.Property(p => p.TaxRate).HasPrecision(6, 4);
            entity.Property(p => p.TaxAmount).HasPrecision(18, 2);
            entity.Property(p => p.TotalAmount).HasPrecision(18, 2);

            entity.HasMany(p => p.Items)
                .WithOne(i => i.PurchaseOrder)
                .HasForeignKey(i => i.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.ToTable("purchase_order_items");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Sku).HasMaxLength(64).IsRequired();
            entity.Property(i => i.Description).HasMaxLength(300).IsRequired();
            entity.Property(i => i.UnitPrice).HasPrecision(18, 2);
            entity.Property(i => i.LineTotal).HasPrecision(18, 2);
        });

        modelBuilder.Entity<AuditDocument>(entity =>
        {
            entity.ToTable("audit_documents");
            entity.HasKey(d => d.Id);
            entity.HasIndex(d => d.Status);
            entity.HasIndex(d => d.CreatedAtUtc);

            entity.Property(d => d.OriginalFileName).HasMaxLength(260).IsRequired();
            entity.Property(d => d.StoredFilePath).HasMaxLength(500).IsRequired();
            entity.Property(d => d.DocumentType).HasConversion<string>().HasMaxLength(32);
            entity.Property(d => d.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(d => d.RiskLevel).HasConversion<string>().HasMaxLength(32);

            entity.Property(d => d.ExtractedOrderNumber).HasMaxLength(80);
            entity.Property(d => d.ExtractedInvoiceNumber).HasMaxLength(80);
            entity.Property(d => d.ExtractedSupplierName).HasMaxLength(220);
            entity.Property(d => d.ExtractedSupplierRuc).HasMaxLength(32);

            entity.Property(d => d.ExtractedSubtotal).HasPrecision(18, 2);
            entity.Property(d => d.ExtractedTaxRate).HasPrecision(6, 4);
            entity.Property(d => d.ExtractedTaxAmount).HasPrecision(18, 2);
            entity.Property(d => d.ExtractedTotalAmount).HasPrecision(18, 2);
            entity.Property(d => d.DetectedVarianceAmount).HasPrecision(18, 2);

            entity.Property(d => d.AiModelUsed).HasMaxLength(80);
            entity.Property(d => d.UploadedByName).HasMaxLength(180);
            entity.Property(d => d.ReviewedByName).HasMaxLength(180);

            entity.HasOne(d => d.MatchedPurchaseOrder)
                .WithMany()
                .HasForeignKey(d => d.MatchedPurchaseOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(d => d.Findings)
                .WithOne(f => f.AuditDocument)
                .HasForeignKey(f => f.AuditDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditFinding>(entity =>
        {
            entity.ToTable("audit_findings");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Code).HasMaxLength(80).IsRequired();
            entity.Property(f => f.Category).HasMaxLength(64).IsRequired();
            entity.Property(f => f.Severity).HasConversion<string>().HasMaxLength(32);
            entity.Property(f => f.Title).HasMaxLength(240).IsRequired();
            entity.Property(f => f.Description).HasMaxLength(1500).IsRequired();
            entity.Property(f => f.ExpectedValue).HasMaxLength(300);
            entity.Property(f => f.ActualValue).HasMaxLength(300);
            entity.Property(f => f.FinancialImpactUsd).HasPrecision(18, 2);
        });
    }
}
