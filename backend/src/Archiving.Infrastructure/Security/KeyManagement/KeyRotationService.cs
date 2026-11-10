using Archiving.Application.Common.Interfaces;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Archiving.Infrastructure.Security.KeyManagement;

public sealed record RotationReport(string NewKekId, int Rewrapped, int Failed, int SkippedShredded, TimeSpan Duration);

public interface IKeyRotationService
{
    /// <summary>Mints a new KEK version and re-wraps every live DEK under it.</summary>
    Task<RotationReport> RotateAsync(CancellationToken ct = default);

    /// <summary>Re-wraps DEKs still sealed under an older KEK, without minting a new version. Use it to
    /// finish an interrupted rotation.</summary>
    Task<RotationReport> ResumeAsync(CancellationToken ct = default);

    /// <summary>How many live objects sit under each KEK version — the report that says whether a retired
    /// key can finally be destroyed.</summary>
    Task<IReadOnlyDictionary<string, int>> KeyUsageAsync(CancellationToken ct = default);
}

/// <summary>
/// Key rotation the way envelope encryption makes possible: re-wrap the data keys, leave the data alone.
///
/// <para>Rotating means unwrapping each DEK under its old KEK and re-sealing it under the new one — a few
/// hundred bytes per object in the metadata index. <b>Not one archived byte is rewritten.</b> Rotating a
/// 40 TB archive costs a table scan, so a 90-day policy is actually operable rather than something
/// everyone agrees to and nobody ever runs.</para>
///
/// <para>Batched and resumable: an interruption leaves a consistent archive with some objects on the old
/// KEK and some on the new, both readable, because every object names its own KEK version. Only when
/// <see cref="KeyUsageAsync"/> shows no live object under a retired version is it safe to destroy it.</para>
/// </summary>
public sealed class KeyRotationService(
    AppDbContext db,
    IKeyManagementService kms,
    IOptions<KmsOptions> options,
    ILogger<KeyRotationService> logger) : IKeyRotationService
{
    public async Task<RotationReport> RotateAsync(CancellationToken ct = default)
    {
        var newKekId = await kms.CreateKekVersionAsync(ct).ConfigureAwait(false);
        logger.LogWarning("Key rotation started — new KEK version {KekId}", newKekId);
        return await RewrapAllAsync(newKekId, ct).ConfigureAwait(false);
    }

    public async Task<RotationReport> ResumeAsync(CancellationToken ct = default)
    {
        var active = kms.ActiveKekId;
        if (string.IsNullOrEmpty(active))
        {
            var health = await kms.CheckHealthAsync(ct).ConfigureAwait(false);
            active = health.ActiveKekId;
        }
        return await RewrapAllAsync(active, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, int>> KeyUsageAsync(CancellationToken ct = default) =>
        await db.ArchivedObjects
            .Where(o => o.ShreddedAtUtc == null)
            .GroupBy(o => o.KekId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct)
            .ConfigureAwait(false);

    private async Task<RotationReport> RewrapAllAsync(string targetKekId, CancellationToken ct)
    {
        var started = DateTime.UtcNow;
        var batchSize = Math.Clamp(options.Value.RewrapBatchSize, 50, 5000);
        int rewrapped = 0, failed = 0;

        // Shredded rows have no key left to re-wrap, and must never be resurrected by rotation.
        var skippedShredded = await db.ArchivedObjects.CountAsync(o => o.ShreddedAtUtc != null, ct).ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            var batch = await db.ArchivedObjects
                .Where(o => o.ShreddedAtUtc == null && o.WrappedDek != null && o.KekId != targetKekId)
                .OrderBy(o => o.Id)
                .Take(batchSize)
                .ToListAsync(ct).ConfigureAwait(false);

            if (batch.Count == 0) break;

            foreach (var row in batch)
            {
                try
                {
                    var (wrapped, kekId) = await kms.RewrapAsync(row.KekId, row.WrappedDek!, ct).ConfigureAwait(false);
                    row.WrappedDek = wrapped;
                    row.KekId = kekId;
                    row.LastRewrappedAtUtc = DateTime.UtcNow;
                    rewrapped++;
                }
                catch (Exception ex)
                {
                    // One unreadable object must not stall the rotation — record it and press on, so the
                    // report ends with an exact list of what still needs attention.
                    failed++;
                    logger.LogError(ex, "Re-wrap failed for object {StorageKey} under KEK {KekId}", row.StorageKey, row.KekId);
                }
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            db.ChangeTracker.Clear();

            // Every row in the batch failed: the KMS is unhealthy, not the data. Stop rather than spin.
            if (rewrapped == 0 && failed >= batch.Count) break;
        }

        var report = new RotationReport(targetKekId, rewrapped, failed, skippedShredded, DateTime.UtcNow - started);
        logger.LogWarning("Key rotation finished: {Rewrapped} re-wrapped, {Failed} failed, target KEK {KekId}",
            report.Rewrapped, report.Failed, report.NewKekId);
        return report;
    }
}
