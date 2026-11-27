using AuditAI.Application.Common.Interfaces;
using AuditAI.Application.Features.Auth;
using AuditAI.Application.Features.Documents;
using AuditAI.Domain.Entities;
using AuditAI.Domain.Enums;
using AuditAI.Infrastructure.Pdf;
using AuditAI.Infrastructure.Persistence;
using AuditAI.Infrastructure.Persistence.Repositories;
using AuditAI.Infrastructure.Security;
using AuditAI.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AuditAI.Tests;

public sealed class InfrastructureAndCqrsTests
{
    [Fact]
    public void NeonConnectionStringHelper_ShouldParseNeonPostgresUriWithSslAndChannelBinding()
    {
        const string uri = "postgresql://neondb_owner:secretPass123@ep-test-pooler.us-east-2.aws.neon.tech/neondb?sslmode=require&channel_binding=require";

        var normalized = NeonConnectionStringHelper.Normalize(uri);
        var builder = new NpgsqlConnectionStringBuilder(normalized);

        Assert.Equal("ep-test-pooler.us-east-2.aws.neon.tech", builder.Host);
        Assert.Equal("neondb", builder.Database);
        Assert.Equal("neondb_owner", builder.Username);
        Assert.Equal("secretPass123", builder.Password);
        Assert.Equal(SslMode.Require, builder.SslMode);
        Assert.Equal(ChannelBinding.Require, builder.ChannelBinding);
    }

    [Fact]
    public async Task QuestPdfGenerator_And_PdfPigExtractor_ShouldGenerateAndExtractRealPdfs()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"auditai_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var generator = new QuestPdfSampleGenerator(tempRoot);
            var extractor = new PdfPigTextExtractor();

            var samples = await generator.EnsureSamplePdfsGeneratedAsync();
            Assert.Equal(5, samples.Count);
            Assert.All(samples, s => Assert.True(s.FileSizeBytes > 1000));

            var invoicePdfPath = Path.Combine(generator.SamplesDirectory, "01_factura_conforme_OC-2026-001.pdf");
            var extractedInvoice = await extractor.ExtractTextAsync(invoicePdfPath);
            Assert.Equal(1, extractedInvoice.PageCount);
            Assert.Contains("OC-2026-001", extractedInvoice.Text);
            Assert.Contains("0992847561001", extractedInvoice.Text);
            Assert.Contains("12650.00", extractedInvoice.Text.Replace(",", ""));

            var contractPdfPath = Path.Combine(generator.SamplesDirectory, "05_contrato_servicios_clausulas_riesgo.pdf");
            var extractedContract = await extractor.ExtractTextAsync(contractPdfPath);
            Assert.Contains("25,000.00", extractedContract.Text);
            Assert.Contains("RENOVACIÓN AUTOMÁTICA FORZOSA", extractedContract.Text);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AuthAndReviewCqrsHandlers_ShouldAuthenticateAndUpdateAuditStatus()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase($"auditai_cqrs_{Guid.NewGuid():N}")
            .Options;

        await using var db = new AuditDbContext(options);
        var hasher = new BcryptPasswordHasher();
        var jwtGen = new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            SecretKey = "UnitTest_SuperSecretKey_Minimum32Characters_2026!"
        }));

        var admin = new User
        {
            Email = "admin@auditai.ec",
            FullName = "Admin Test",
            PasswordHash = hasher.Hash("Admin2026!"),
            Role = UserRole.Admin
        };
        db.Users.Add(admin);

        var flaggedDoc = new AuditDocument
        {
            OriginalFileName = "factura_sobreprecio.pdf",
            StoredFilePath = "dummy.pdf",
            Status = AuditStatus.FlaggedDiscrepancy,
            RiskLevel = RiskLevel.High,
            UploadedByUserId = admin.Id,
            UploadedByName = admin.FullName
        };
        db.AuditDocuments.Add(flaggedDoc);
        await db.SaveChangesAsync();

        var userRepo = new UserRepository(db);
        var loginHandler = new LoginCommandHandler(userRepo, hasher, jwtGen);
        var authRes = await loginHandler.HandleAsync(new LoginCommand("admin@auditai.ec", "Admin2026!"));

        Assert.False(string.IsNullOrWhiteSpace(authRes.AccessToken));
        Assert.Equal("Admin", authRes.User.Role);

        var notifier = new RecordingNotifier();
        var docRepo = new AuditDocumentRepository(db);
        var reviewHandler = new ReviewAuditDocumentCommandHandler(docRepo, db, notifier);

        var updatedDetail = await reviewHandler.HandleAsync(new ReviewAuditDocumentCommand(
            flaggedDoc.Id,
            Approve: false,
            Notes: "Rechazado por sobreprecio de $500 no autorizado.",
            ReviewerUserId: admin.Id,
            ReviewerName: admin.FullName));

        Assert.Equal("Rejected", updatedDetail.Status);
        Assert.Equal("Rechazado por sobreprecio de $500 no autorizado.", updatedDetail.ReviewNotes);
        Assert.Equal(1, notifier.NotificationsCount);
    }

    [Fact]
    public async Task UploadAuditDocumentCommand_ShouldSavePdfAndEnqueueDocumentId()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"auditai_upload_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var options = new DbContextOptionsBuilder<AuditDbContext>()
                .UseInMemoryDatabase($"auditai_upload_db_{Guid.NewGuid():N}")
                .Options;

            await using var db = new AuditDbContext(options);
            var storage = new LocalPdfStorageService(tempRoot);
            var queue = new AuditChannelQueue();
            var notifier = new RecordingNotifier();
            var docRepo = new AuditDocumentRepository(db);

            var handler = new UploadAuditDocumentCommandHandler(storage, docRepo, db, queue, notifier);
            await using var pdfStream = new MemoryStream("%PDF-1.7 dummy test content"u8.ToArray());

            var summary = await handler.HandleAsync(new UploadAuditDocumentCommand(
                "contrato_marco.pdf",
                pdfStream,
                Guid.NewGuid(),
                "Carlos Analista"));

            Assert.Equal("Queued", summary.Status);
            Assert.Equal("Contract", summary.DocumentType);

            var dequeuedId = await queue.DequeueAsync();
            Assert.Equal(summary.Id, dequeuedId);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private sealed class RecordingNotifier : IAuditRealtimeNotifier
    {
        public int NotificationsCount { get; private set; }

        public Task NotifyDocumentUpdatedAsync(AuditDocument document, string stageMessage, CancellationToken ct = default)
        {
            NotificationsCount++;
            return Task.CompletedTask;
        }
    }
}
