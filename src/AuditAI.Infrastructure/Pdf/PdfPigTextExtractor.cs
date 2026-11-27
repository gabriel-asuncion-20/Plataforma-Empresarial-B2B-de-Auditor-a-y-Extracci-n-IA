using System.Text;
using AuditAI.Application.Common.Interfaces;
using AuditAI.Application.DTOs;
using UglyToad.PdfPig;

namespace AuditAI.Infrastructure.Pdf;

public sealed class PdfPigTextExtractor : IPdfTextExtractor
{
    public Task<ExtractedPdfResult> ExtractTextAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"No se encontró el archivo PDF en disco: {filePath}");
        }

        using var document = PdfDocument.Open(filePath);
        var sb = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            sb.AppendLine($"--- PÁGINA {page.Number} ---");

            var words = page.GetWords().ToList();
            if (words.Count > 0)
            {
                var lines = words
                    .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 4.0) * 4.0)
                    .OrderByDescending(g => g.Key);

                foreach (var line in lines)
                {
                    var lineText = string.Join(" ", line.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text));
                    if (!string.IsNullOrWhiteSpace(lineText))
                    {
                        sb.AppendLine(lineText);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(page.Text))
            {
                sb.AppendLine(page.Text.Trim());
            }

            sb.AppendLine();
        }

        return Task.FromResult(new ExtractedPdfResult(sb.ToString().Trim(), document.NumberOfPages));
    }
}
