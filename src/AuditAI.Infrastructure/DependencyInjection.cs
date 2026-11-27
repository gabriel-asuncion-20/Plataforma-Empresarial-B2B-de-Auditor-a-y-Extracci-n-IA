using AuditAI.Application.Common.Interfaces;
using AuditAI.Infrastructure.AI;
using AuditAI.Infrastructure.Background;
using AuditAI.Infrastructure.Pdf;
using AuditAI.Infrastructure.Persistence;
using AuditAI.Infrastructure.Persistence.Repositories;
using AuditAI.Infrastructure.Security;
using AuditAI.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AuditAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<GeminiOptions>(configuration.GetSection(GeminiOptions.SectionName));

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
        services.AddSingleton<IPdfTextExtractor, PdfPigTextExtractor>();

        services.AddHttpClient<GeminiStructuredDocumentAnalyzer>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
            return Ipv4SocketsHttpHandlerFactory.Create(opts.PreferIpv4);
        });

        services.AddSingleton<IAiDocumentAnalyzer>(sp => sp.GetRequiredService<GeminiStructuredDocumentAnalyzer>());
        services.AddSingleton<IChatClient>(sp => sp.GetRequiredService<GeminiStructuredDocumentAnalyzer>());

        services.AddHostedService<AuditBackgroundWorker>();

        return services;
    }
}
