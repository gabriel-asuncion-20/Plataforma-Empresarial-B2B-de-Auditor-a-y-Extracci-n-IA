using AuditAI.Application.Common.Cqrs;
using AuditAI.Application.Common.Interfaces;
using AuditAI.Application.DTOs;
using AuditAI.Domain.Entities;
using AuditAI.Domain.Enums;

namespace AuditAI.Application.Features.Documents;

public sealed record UploadAuditDocumentCommand(
    string FileName,
    Stream Content,
    Guid UploadedByUserId,
    string UploadedByName) : ICommand<AuditDocumentSummaryDto>;

public sealed class UploadAuditDocumentCommandHandler(
    IPdfStorageService pdfStorageService,
    IAuditDocumentRepository documentRepository,
    IUnitOfWork unitOfWork,
    IAuditQueue auditQueue,
    IAuditRealtimeNotifier realtimeNotifier) : ICommandHandler<UploadAuditDocumentCommand, AuditDocumentSummaryDto>
{
    public async Task<AuditDocumentSummaryDto> HandleAsync(UploadAuditDocumentCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.FileName) ||
            !command.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Solo se admiten archivos en formato PDF (.pdf).");
        }

        var (storedPath, sizeBytes) = await pdfStorageService.SaveUploadedPdfAsync(command.FileName, command.Content, ct);
        if (sizeBytes <= 0)
        {
            throw new ArgumentException("El archivo PDF subido está vacío.");
        }

        var inferredType = command.FileName.Contains("contrato", StringComparison.OrdinalIgnoreCase) ||
                           command.FileName.Contains("contract", StringComparison.OrdinalIgnoreCase)
            ? DocumentType.Contract
            : DocumentType.Invoice;

        var doc = new AuditDocument
        {
            OriginalFileName = Path.GetFileName(command.FileName),
            StoredFilePath = storedPath,
            FileSizeBytes = sizeBytes,
            DocumentType = inferredType,
            Status = AuditStatus.Queued,
            RiskLevel = RiskLevel.Low,
            UploadedByUserId = command.UploadedByUserId,
            UploadedByName = command.UploadedByName,
            CreatedAtUtc = DateTime.UtcNow
        };

        await documentRepository.AddAsync(doc, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await auditQueue.EnqueueAsync(doc.Id, ct);
        await realtimeNotifier.NotifyDocumentUpdatedAsync(doc, "Documento recibido y encolado para auditoría IA.", ct);

        return doc.ToSummaryDto();
    }
}

public sealed record EnqueueSamplePdfCommand(
    string SampleFileName,
    Guid UploadedByUserId,
    string UploadedByName) : ICommand<AuditDocumentSummaryDto>;

public sealed class EnqueueSamplePdfCommandHandler(
    IPdfStorageService pdfStorageService,
    IAuditDocumentRepository documentRepository,
    IUnitOfWork unitOfWork,
    IAuditQueue auditQueue,
    IAuditRealtimeNotifier realtimeNotifier) : ICommandHandler<EnqueueSamplePdfCommand, AuditDocumentSummaryDto>
{
    public async Task<AuditDocumentSummaryDto> HandleAsync(EnqueueSamplePdfCommand command, CancellationToken ct = default)
    {
        var (storedPath, sizeBytes) = await pdfStorageService.CopySamplePdfToStorageAsync(command.SampleFileName, ct);

        var inferredType = command.SampleFileName.Contains("contrato", StringComparison.OrdinalIgnoreCase)
            ? DocumentType.Contract
            : DocumentType.Invoice;

        var doc = new AuditDocument
        {
            OriginalFileName = Path.GetFileName(command.SampleFileName),
            StoredFilePath = storedPath,
            FileSizeBytes = sizeBytes,
            DocumentType = inferredType,
            Status = AuditStatus.Queued,
            RiskLevel = RiskLevel.Low,
            UploadedByUserId = command.UploadedByUserId,
            UploadedByName = command.UploadedByName,
            CreatedAtUtc = DateTime.UtcNow
        };

        await documentRepository.AddAsync(doc, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await auditQueue.EnqueueAsync(doc.Id, ct);
        await realtimeNotifier.NotifyDocumentUpdatedAsync(doc, "Documento de prueba encolado para procesamiento asíncrono.", ct);

        return doc.ToSummaryDto();
    }
}

public sealed record ReviewAuditDocumentCommand(
    Guid DocumentId,
    bool Approve,
    string? Notes,
    Guid ReviewerUserId,
    string ReviewerName) : ICommand<AuditDocumentDetailDto>;

public sealed class ReviewAuditDocumentCommandHandler(
    IAuditDocumentRepository documentRepository,
    IUnitOfWork unitOfWork,
    IAuditRealtimeNotifier realtimeNotifier) : ICommandHandler<ReviewAuditDocumentCommand, AuditDocumentDetailDto>
{
    public async Task<AuditDocumentDetailDto> HandleAsync(ReviewAuditDocumentCommand command, CancellationToken ct = default)
    {
        var doc = await documentRepository.GetByIdWithDetailsAsync(command.DocumentId, ct)
            ?? throw new KeyNotFoundException($"No se encontró el documento de auditoría {command.DocumentId}.");

        if (command.Approve)
        {
            doc.ApproveManually(command.ReviewerUserId, command.ReviewerName, command.Notes);
        }
        else
        {
            doc.Reject(command.ReviewerUserId, command.ReviewerName, command.Notes);
        }

        await unitOfWork.SaveChangesAsync(ct);

        var actionText = command.Approve
            ? $"Auditoría aprobada manualmente por {command.ReviewerName}."
            : $"Documento rechazado por {command.ReviewerName}.";
        await realtimeNotifier.NotifyDocumentUpdatedAsync(doc, actionText, ct);

        return doc.ToDetailDto();
    }
}
