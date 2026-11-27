using System.Threading.Channels;
using AuditAI.Application.Common.Interfaces;

namespace AuditAI.Infrastructure.Services;

public sealed class AuditChannelQueue : IAuditQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(500)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(Guid documentId, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(documentId, ct);

    public ValueTask<Guid> DequeueAsync(CancellationToken ct = default)
        => _channel.Reader.ReadAsync(ct);
}

public sealed class LocalPdfStorageService : IPdfStorageService
{
    private readonly string _uploadsDir;
    private readonly string _samplesDir;

    public LocalPdfStorageService(string contentRootPath)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(contentRootPath, "..", ".."));
        if (!Directory.Exists(Path.Combine(repoRoot, "src")))
        {
            repoRoot = contentRootPath;
        }

        _uploadsDir = Path.Combine(repoRoot, "storage", "uploads");
        _samplesDir = Path.Combine(repoRoot, "sample-pdfs");
        Directory.CreateDirectory(_uploadsDir);
        Directory.CreateDirectory(_samplesDir);
    }

    public async Task<(string StoredPath, long SizeBytes)> SaveUploadedPdfAsync(
        string originalFileName,
        Stream content,
        CancellationToken ct = default)
    {
        var safeName = Path.GetFileNameWithoutExtension(originalFileName);
        safeName = string.Concat(safeName.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "documento";
        }

        var uniqueName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}_{safeName}.pdf";
        var fullPath = Path.Combine(_uploadsDir, uniqueName);

        await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await content.CopyToAsync(fs, ct);
        }

        var info = new FileInfo(fullPath);
        return (fullPath, info.Length);
    }

    public async Task<(string StoredPath, long SizeBytes)> CopySamplePdfToStorageAsync(
        string sampleFileName,
        CancellationToken ct = default)
    {
        var cleanFileName = Path.GetFileName(sampleFileName);
        var sourcePath = Path.Combine(_samplesDir, cleanFileName);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"El archivo de prueba '{cleanFileName}' no existe en /sample-pdfs.");
        }

        await using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return await SaveUploadedPdfAsync(cleanFileName, sourceStream, ct);
    }
}
