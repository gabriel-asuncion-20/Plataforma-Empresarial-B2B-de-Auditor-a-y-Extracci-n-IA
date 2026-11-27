using AuditAI.Application.Common.Interfaces;
using AuditAI.Infrastructure.Persistence;
using AuditAI.Infrastructure.Persistence.Repositories;
using AuditAI.Infrastructure.Security;
using AuditAI.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuditAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var rawConn = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(rawConn) || rawConn.Contains("PEGA_AQUI", StringComparison.OrdinalIgnoreCase))
        {
            rawConn = Environment.GetEnvironmentVariable("DATABASE_URL");
        }

        var normalizedConn = NeonConnectionStringHelper.Normalize(rawConn);

        services.AddDbContext<AuditDbContext>(options =>
        {
            options.UseNpgsql(normalizedConn, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", AuditDbContext.SchemaName);
                npgsql.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
            });
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AuditDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<IAuditDocumentRepository, AuditDocumentRepository>();

        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IAuditQueue, AuditChannelQueue>();
        services.AddSingleton<IPdfStorageService>(_ => new LocalPdfStorageService(contentRootPath));

        return services;
    }
}
