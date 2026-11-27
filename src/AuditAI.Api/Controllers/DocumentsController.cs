using System.Security.Claims;
using AuditAI.Application.Common.Cqrs;
using AuditAI.Application.Common.Interfaces;
using AuditAI.Application.DTOs;
using AuditAI.Application.Features.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditAI.Api.Controllers;

public sealed record ReviewDocumentRequestDto(bool Approve, string? Notes);

[ApiController]
[Route("api/documents")]
[Authorize]
public sealed class DocumentsController(
    ICqrsDispatcher dispatcher,
    IAuditDocumentRepository documentRepository,
    ISamplePdfGenerator samplePdfGenerator) : ControllerBase
{
    [HttpGet("samples")]
    public ActionResult<IReadOnlyList<SamplePdfInfoDto>> GetSamples()
        => Ok(samplePdfGenerator.GetAvailableSamplePdfs());

    [HttpGet("samples/{fileName}/download")]
    [AllowAnonymous]
    public IActionResult DownloadSample(string fileName)
    {
        var cleanName = Path.GetFileName(fileName);
        var repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
        if (!Directory.Exists(Path.Combine(repoRoot, "sample-pdfs")))
        {
            repoRoot = Directory.GetCurrentDirectory();
        }

        var path = Path.Combine(repoRoot, "sample-pdfs", cleanName);
        if (!System.IO.File.Exists(path))
        {
            return NotFound(new { message = "PDF de muestra no encontrado." });
        }

        return PhysicalFile(path, "application/pdf", cleanName);
    }

    [HttpPost("samples/{fileName}/enqueue")]
    public async Task<ActionResult<AuditDocumentSummaryDto>> EnqueueSample(string fileName, CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = Guid.TryParse(userIdStr, out var parsed) ? parsed : Guid.Empty;
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? "Analista";

        try
        {
            var summary = await dispatcher.SendCommandAsync<EnqueueSamplePdfCommand, AuditDocumentSummaryDto>(
                new EnqueueSamplePdfCommand(fileName, userId, userName), ct);
            return Accepted(summary);
        }
        catch (FileNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditDocumentSummaryDto>>> GetAll(CancellationToken ct)
    {
        var docs = await dispatcher.SendQueryAsync<GetAuditDocumentsQuery, IReadOnlyList<AuditDocumentSummaryDto>>(
            new GetAuditDocumentsQuery(), ct);
        return Ok(docs);
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<DashboardMetricsDto>> GetMetrics(CancellationToken ct)
    {
        var metrics = await dispatcher.SendQueryAsync<GetDashboardMetricsQuery, DashboardMetricsDto>(
            new GetDashboardMetricsQuery(), ct);
        return Ok(metrics);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AuditDocumentDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var detail = await dispatcher.SendQueryAsync<GetAuditDocumentByIdQuery, AuditDocumentDetailDto?>(
            new GetAuditDocumentByIdQuery(id), ct);

        return detail is null ? NotFound(new { message = "Documento no encontrado." }) : Ok(detail);
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken ct)
    {
        var doc = await documentRepository.GetByIdWithDetailsAsync(id, ct);
        if (doc is null || !System.IO.File.Exists(doc.StoredFilePath))
        {
            return NotFound(new { message = "Archivo físico PDF no disponible." });
        }

        var stream = new FileStream(doc.StoredFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, "application/pdf", enableRangeProcessing: true);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<IReadOnlyList<AuditDocumentSummaryDto>>> Upload(
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        if (files is null || files.Count == 0)
        {
            if (Request.Form.Files.Count > 0)
            {
                files = Request.Form.Files.ToList();
            }
            else
            {
                return BadRequest(new { message = "Debes adjuntar al menos un archivo PDF." });
            }
        }

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = Guid.TryParse(userIdStr, out var parsed) ? parsed : Guid.Empty;
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? "Analista";

        var enqueued = new List<AuditDocumentSummaryDto>();
        foreach (var file in files)
        {
            await using var stream = file.OpenReadStream();
            var summary = await dispatcher.SendCommandAsync<UploadAuditDocumentCommand, AuditDocumentSummaryDto>(
                new UploadAuditDocumentCommand(file.FileName, stream, userId, userName), ct);
            enqueued.Add(summary);
        }

        return Accepted(new
        {
            message = $"{enqueued.Count} documento(s) recibido(s) y encolado(s) en segundo plano (202 Accepted).",
            documents = enqueued
        });
    }

    [HttpPost("{id:guid}/review")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AuditDocumentDetailDto>> Review(
        Guid id,
        [FromBody] ReviewDocumentRequestDto request,
        CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = Guid.TryParse(userIdStr, out var parsed) ? parsed : Guid.Empty;
        var userName = User.FindFirstValue(ClaimTypes.Name) ?? "Administrador";

        try
        {
            var updated = await dispatcher.SendCommandAsync<ReviewAuditDocumentCommand, AuditDocumentDetailDto>(
                new ReviewAuditDocumentCommand(id, request.Approve, request.Notes, userId, userName), ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
