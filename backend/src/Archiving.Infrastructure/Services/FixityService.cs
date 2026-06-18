using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Preservation;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>Verifies stored files against the checksum recorded at ingest (ISO 16363 fixity).
/// Each check is logged to <see cref="FixityCheck"/> and to the tamper-evident audit trail.</summary>
public sealed class FixityService(
    AppDbContext db,
    IFileStorage storage,
    ICurrentUser currentUser,
    IAuditWriter audit) : IFixityService
{
    public async Task<Result<FixityCheckDto>> VerifyAttachmentAsync(long attachmentId, CancellationToken ct = default)
    {
        var att = await db.DocumentAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId, ct);
        if (att is null) return Result<FixityCheckDto>.Fail("المرفق غير موجود");

        var check = await RunCheckAsync(att, currentUser.UserId, ct);
        await db.SaveChangesAsync(ct);
        await AuditCheckAsync(att, check, ct);
        return Result<FixityCheckDto>.Ok(ToDto(check, att.DocumentId, att.FileName));
    }

    public async Task<IReadOnlyList<FixityCheckDto>> RecentChecksAsync(int take = 100, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);
        return await db.FixityChecks
            .OrderByDescending(c => c.Id).Take(take)
            .Select(c => new FixityCheckDto(
                c.Id, c.DocumentAttachmentId, c.DocumentAttachment.DocumentId, c.DocumentAttachment.FileName,
                c.Algorithm, c.Result.ToString(), c.CheckedAt, c.CheckedByUserId, c.Note))
            .ToListAsync(ct);
    }

    public async Task<int> SweepAsync(int max, CancellationToken ct = default)
    {
        max = Math.Clamp(max, 1, 1000);
        // Least-recently-checked first (never-checked rows have null and sort first).
        var batch = await db.DocumentAttachments
            .Where(a => a.Checksum != null)
            .OrderBy(a => a.LastFixityCheckAt).ThenBy(a => a.Id)
            .Take(max).ToListAsync(ct);

        var failures = 0;
        foreach (var att in batch)
        {
            var check = await RunCheckAsync(att, null, ct);
            await db.SaveChangesAsync(ct);
            await AuditCheckAsync(att, check, ct);
            if (check.Result is FixityResult.Failed or FixityResult.Missing) failures++;
        }
        return failures;
    }

    // ---- helpers ----

    private async Task<FixityCheck> RunCheckAsync(DocumentAttachment att, long? byUser, CancellationToken ct)
    {
        var check = new FixityCheck
        {
            DocumentAttachmentId = att.Id,
            Algorithm = att.ChecksumAlgorithm,
            ExpectedHash = att.Checksum,
            CheckedAt = DateTime.UtcNow,
            CheckedByUserId = byUser,
        };

        if (string.IsNullOrEmpty(att.Checksum))
        {
            check.Result = FixityResult.NoBaseline;
            check.Note = "لا توجد قيمة تجزئة مسجّلة عند الإدخال";
        }
        else
        {
            var stream = await storage.OpenAsync(att.StorageKey, ct);
            if (stream is null)
            {
                check.Result = FixityResult.Missing;
                check.Note = "تعذّر العثور على الملف في وحدة التخزين";
            }
            else
            {
                await using (stream)
                {
                    check.ActualHash = await FileHasher.Sha256HexAsync(stream, ct);
                }
                check.Result = string.Equals(check.ActualHash, att.Checksum, StringComparison.OrdinalIgnoreCase)
                    ? FixityResult.Verified : FixityResult.Failed;
            }
        }

        att.LastFixityCheckAt = check.CheckedAt;
        db.FixityChecks.Add(check);
        return check;
    }

    private async Task AuditCheckAsync(DocumentAttachment att, FixityCheck check, CancellationToken ct)
    {
        var action = check.Result switch
        {
            FixityResult.Verified => "FixityVerified",
            FixityResult.Failed => "FixityFailed",
            FixityResult.Missing => "FixityMissing",
            _ => "FixityNoBaseline",
        };
        await audit.WriteAsync(action, "DocumentAttachment", att.Id, att.FileName,
            oldValues: check.ExpectedHash, newValues: check.ActualHash, ct: ct);
    }

    private static FixityCheckDto ToDto(FixityCheck c, long documentId, string fileName) =>
        new(c.Id, c.DocumentAttachmentId, documentId, fileName, c.Algorithm, c.Result.ToString(),
            c.CheckedAt, c.CheckedByUserId, c.Note);
}
