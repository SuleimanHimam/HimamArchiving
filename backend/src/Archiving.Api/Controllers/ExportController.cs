using System.IO.Compression;
using System.Text;
using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

/// <summary>Download documents (with their files) as ZIP archives — single document or in bulk.</summary>
[ApiController]
[Route("api/documents")]
[Authorize]
[HasPermission("Documents.View")]
public sealed class ExportController(AppDbContext db, IFileStorage storage, ICurrentUser currentUser) : ControllerBase
{
    private static string Safe(string s) => string.Concat(s.Split(Path.GetInvalidFileNameChars())).Trim();

    // Single document → ZIP of its attachments.
    [HttpGet("{id:long}/zip")]
    public async Task<IActionResult> DocumentZip(long id, CancellationToken ct)
    {
        var doc = await db.Documents.Include(d => d.Attachments).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return NotFound();
        var shared = await db.DocumentShares.AnyAsync(s => s.DocumentId == id && s.SharedWithUserId == currentUser.UserId, ct);
        if ((int)doc.Confidentiality > (int)currentUser.Clearance && !shared) return Forbid();

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            await AddDocumentAsync(zip, doc.DocumentNumber, doc.Attachments, "", ct);
        ms.Position = 0;
        return File(ms, "application/zip", $"{Safe(doc.DocumentNumber)}.zip");
    }

    // Bulk: every document the caller can see (optionally only favorites / a folder), zipped per document.
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] bool favoritesOnly, [FromQuery] long? folderId, CancellationToken ct)
    {
        var q = db.Documents.Include(d => d.Attachments)
            .Where(d => d.IsLatestVersion && (int)d.Confidentiality <= (int)currentUser.Clearance);
        if (favoritesOnly) q = q.Where(d => db.DocumentFavorites.Any(f => f.DocumentId == d.Id && f.UserId == currentUser.UserId));
        if (folderId is { } fid) q = q.Where(d => d.FolderId == fid);

        var docs = await q.OrderBy(d => d.DocumentNumber).Take(500).ToListAsync(ct);

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var manifest = new StringBuilder("سجل التصدير\n=============\n");
            foreach (var d in docs)
            {
                manifest.AppendLine($"{d.DocumentNumber}\t{d.Title}\t({d.Attachments.Count} ملف)");
                await AddDocumentAsync(zip, d.DocumentNumber, d.Attachments, $"{Safe(d.DocumentNumber)}/", ct);
            }
            var entry = zip.CreateEntry("manifest.txt");
            await using var es = entry.Open();
            await es.WriteAsync(Encoding.UTF8.GetBytes(manifest.ToString()), ct);
        }
        ms.Position = 0;
        return File(ms, "application/zip", $"documents-export-{DateTime.UtcNow:yyyyMMdd-HHmm}.zip");
    }

    private async Task AddDocumentAsync(ZipArchive zip, string docNumber, IEnumerable<Domain.Entities.DocumentAttachment> atts, string prefix, CancellationToken ct)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in atts)
        {
            await using var src = await storage.OpenAsync(a.StorageKey, ct);
            if (src is null) continue;
            var name = Safe(a.FileName);
            if (!seen.Add(name)) name = $"{Path.GetFileNameWithoutExtension(name)}-{a.Id}{Path.GetExtension(name)}";
            var entry = zip.CreateEntry(prefix + name, CompressionLevel.Fastest);
            await using var dst = entry.Open();
            await src.CopyToAsync(dst, ct);
        }
    }
}
