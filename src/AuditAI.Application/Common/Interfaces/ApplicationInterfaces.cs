using AuditAI.Application.DTOs;
using AuditAI.Domain.Entities;
using AuditAI.Domain.Enums;

namespace AuditAI.Application.Common.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetFirstByRoleAsync(UserRole role, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
}

public interface IPurchaseOrderRepository
{
    Task<IReadOnlyList<PurchaseOrder>> GetAllWithItemsAsync(CancellationToken ct = default);
    Task<PurchaseOrder?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
}

public interface IAuditDocumentRepository
{
    Task AddAsync(AuditDocument document, CancellationToken ct = default);
    Task<AuditDocument?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AuditDocument>> GetAllWithFindingsAsync(CancellationToken ct = default);
    Task ReplaceFindingsAsync(Guid documentId, IEnumerable<AuditFinding> findings, CancellationToken ct = default);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) GenerateToken(User user);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IPdfStorageService
{
    Task<(string StoredPath, long SizeBytes)> SaveUploadedPdfAsync(string originalFileName, Stream content, CancellationToken ct = default);
    Task<(string StoredPath, long SizeBytes)> CopySamplePdfToStorageAsync(string sampleFileName, CancellationToken ct = default);
}

public interface IPdfTextExtractor
{
    Task<ExtractedPdfResult> ExtractTextAsync(string filePath, CancellationToken ct = default);
}

public interface IAiDocumentAnalyzer
{
    Task<DocumentAiAnalysisResult> AnalyzeDocumentAsync(string fileName, string rawText, CancellationToken ct = default);
}

public interface IAuditQueue
{
    ValueTask EnqueueAsync(Guid documentId, CancellationToken ct = default);
    ValueTask<Guid> DequeueAsync(CancellationToken ct = default);
}

public interface IAuditRealtimeNotifier
{
    Task NotifyDocumentUpdatedAsync(AuditDocument document, string stageMessage, CancellationToken ct = default);
}

public interface ISamplePdfGenerator
{
    Task<IReadOnlyList<SamplePdfInfoDto>> EnsureSamplePdfsGeneratedAsync(CancellationToken ct = default);
    IReadOnlyList<SamplePdfInfoDto> GetAvailableSamplePdfs();
}
