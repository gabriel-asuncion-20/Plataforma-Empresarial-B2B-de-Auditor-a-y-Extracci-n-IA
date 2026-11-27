using AuditAI.Application.Common.Interfaces;
using AuditAI.Domain.Entities;
using AuditAI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AuditAI.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");
        var db = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var samplePdfGenerator = scope.ServiceProvider.GetRequiredService<ISamplePdfGenerator>();

        if (db.Database.IsRelational())
        {
            logger.LogInformation("Aplicando migraciones de EF Core sobre Neon PostgreSQL (esquema {Schema})...", AuditDbContext.SchemaName);
            await db.Database.MigrateAsync(ct);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(ct);
        }

        await SeedUsersAsync(db, passwordHasher, logger, ct);
        await SeedPurchaseOrdersAsync(db, logger, ct);

        var samples = await samplePdfGenerator.EnsureSamplePdfsGeneratedAsync(ct);
        logger.LogInformation("Generador PDF verificado: {Count} archivos PDF reales listos en /sample-pdfs.", samples.Count);
    }

    private static async Task SeedUsersAsync(
        AuditDbContext db,
        IPasswordHasher passwordHasher,
        ILogger logger,
        CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        var users = new[]
        {
            new User
            {
                Email = "admin@auditai.ec",
                FullName = "Valeria Mendoza (Contralora General)",
                PasswordHash = passwordHasher.Hash("Admin2026!"),
                Role = UserRole.Admin,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            },
            new User
            {
                Email = "analista@auditai.ec",
                FullName = "Carlos Viteri (Analista Cuentas por Pagar)",
                PasswordHash = passwordHasher.Hash("Analyst2026!"),
                Role = UserRole.Analyst,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            }
        };

        await db.Users.AddRangeAsync(users, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Usuarios semilla creados (Admin y Analyst).");
    }

    private static async Task SeedPurchaseOrdersAsync(
        AuditDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        if (await db.PurchaseOrders.AnyAsync(ct))
        {
            return;
        }

        var now = DateTime.UtcNow;

        var orders = new List<PurchaseOrder>
        {
            new()
            {
                OrderNumber = "OC-2026-001",
                SupplierName = "TecnoAndina Soluciones S.A.",
                SupplierRuc = "0992847561001",
                Currency = "USD",
                Subtotal = 11000.00m,
                TaxRate = 0.15m,
                TaxAmount = 1650.00m,
                TotalAmount = 12650.00m,
                IssuedAtUtc = now.AddDays(-12),
                ExpectedDeliveryUtc = now.AddDays(3),
                Status = "Approved",
                Items =
                [
                    new PurchaseOrderItem
                    {
                        Sku = "SRV-DELL-R650",
                        Description = "Servidor Rack Empresarial Dell PowerEdge R650 Dual Xeon",
                        Quantity = 2,
                        UnitPrice = 4200.00m,
                        LineTotal = 8400.00m
                    },
                    new PurchaseOrderItem
                    {
                        Sku = "SW-FORTI-200F",
                        Description = "Appliance Firewall Corporativo FortiGate 200F + UTM 1 Año",
                        Quantity = 2,
                        UnitPrice = 1300.00m,
                        LineTotal = 2600.00m
                    }
                ]
            },
            new()
            {
                OrderNumber = "OC-2026-002",
                SupplierName = "CloudLatam Infraestructura Cia. Ltda.",
                SupplierRuc = "1792458312001",
                Currency = "USD",
                Subtotal = 4000.00m,
                TaxRate = 0.15m,
                TaxAmount = 600.00m,
                TotalAmount = 4600.00m,
                IssuedAtUtc = now.AddDays(-9),
                ExpectedDeliveryUtc = now.AddDays(5),
                Status = "Approved",
                Items =
                [
                    new PurchaseOrderItem
                    {
                        Sku = "LIC-M365-E5",
                        Description = "Suscripción Corporativa Seguridad Cloud E5 (Asientos Anuales)",
                        Quantity = 50,
                        UnitPrice = 48.00m,
                        LineTotal = 2400.00m
                    },
                    new PurchaseOrderItem
                    {
                        Sku = "SEC-SOC-MON",
                        Description = "Servicio Mensual Monitoreo SOC 24/7 y Respuesta a Incidentes",
                        Quantity = 1,
                        UnitPrice = 1600.00m,
                        LineTotal = 1600.00m
                    }
                ]
            },
            new()
            {
                OrderNumber = "OC-2026-003",
                SupplierName = "Logística Pacífico Express S.A.",
                SupplierRuc = "0993124578001",
                Currency = "USD",
                Subtotal = 3800.00m,
                TaxRate = 0.15m,
                TaxAmount = 570.00m,
                TotalAmount = 4370.00m,
                IssuedAtUtc = now.AddDays(-7),
                ExpectedDeliveryUtc = now.AddDays(2),
                Status = "Approved",
                Items =
                [
                    new PurchaseOrderItem
                    {
                        Sku = "LOG-GYE-UIO",
                        Description = "Flete Corporativo Refrigerado Ruta Guayaquil - Quito",
                        Quantity = 4,
                        UnitPrice = 850.00m,
                        LineTotal = 3400.00m
                    },
                    new PurchaseOrderItem
                    {
                        Sku = "SEG-CARGA-B2B",
                        Description = "Póliza de Seguro Integral de Mercadería en Tránsito",
                        Quantity = 1,
                        UnitPrice = 400.00m,
                        LineTotal = 400.00m
                    }
                ]
            },
            new()
            {
                OrderNumber = "OC-2026-004",
                SupplierName = "Suministros Industriales Austro S.A.",
                SupplierRuc = "0190482915001",
                Currency = "USD",
                Subtotal = 4500.00m,
                TaxRate = 0.15m,
                TaxAmount = 675.00m,
                TotalAmount = 5175.00m,
                IssuedAtUtc = now.AddDays(-5),
                ExpectedDeliveryUtc = now.AddDays(10),
                Status = "Approved",
                Items =
                [
                    new PurchaseOrderItem
                    {
                        Sku = "IND-UPS-10KVA",
                        Description = "Sistema UPS Trifásico Online Doble Conversión 10kVA",
                        Quantity = 3,
                        UnitPrice = 1500.00m,
                        LineTotal = 4500.00m
                    }
                ]
            }
        };

        await db.PurchaseOrders.AddRangeAsync(orders, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("4 Órdenes de Compra B2B sembradas en Neon PostgreSQL.");
    }
}
