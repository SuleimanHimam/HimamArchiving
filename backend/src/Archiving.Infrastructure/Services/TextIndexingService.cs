using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Services;

/// <summary>Extracts searchable text from stored attachments and records the result + status.</summary>
public sealed class TextIndexingService(
    AppDbContext db,
    IFileStorage storage,
    ITextExtractionService extractor,
    ILogger<TextIndexingService> log) : ITextIndexingService
{
    public async Task<int> SweepPendingAsync(int batch, CancellationToken ct = default)
    {
        var pending = await db.DocumentAttachments
            .Where(a => a.ExtractionStatus == TextExtractionStatus.Pending)
            .OrderBy(a => a.Id)
            .Take(batch)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var a in pending)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!extractor.CanExtract(a.FileExtension))
                {
                    a.ExtractionStatus = TextExtractionStatus.Skipped;
                }
                else
                {
                    var bytes = await ReadAsync(a.StorageKey, ct);
                    if (bytes is null)
                    {
                        a.ExtractionStatus = TextExtractionStatus.Failed;
                    }
                    else
                    {
                        var r = await extractor.ExtractAsync(bytes, a.FileExtension, ct);
                        a.ExtractedText = string.IsNullOrWhiteSpace(r.Text) ? null : r.Text;
                        a.ExtractionSource = r.Source;
                        a.ExtractionStatus = TextExtractionStatus.Done;
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                log.LogError(ex, "Text extraction failed for attachment {Id}", a.Id);
                a.ExtractionStatus = TextExtractionStatus.Failed;
            }

            a.TextExtractedAt = DateTime.UtcNow;
            processed++;
            await db.SaveChangesAsync(ct); // persist per item so a later failure doesn't discard earlier work
        }
        return processed;
    }

    public async Task<int> ReindexAllAsync(CancellationToken ct = default) =>
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE DocumentAttachments SET ExtractionStatus = 0, ExtractedText = NULL, ExtractionSource = NULL, TextExtractedAt = NULL", ct);

    private async Task<byte[]?> ReadAsync(string key, CancellationToken ct)
    {
        await using var s = await storage.OpenAsync(key, ct);
        if (s is null) return null;
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
