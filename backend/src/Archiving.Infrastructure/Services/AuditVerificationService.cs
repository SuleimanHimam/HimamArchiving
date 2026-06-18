using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Preservation;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>Loads the audit log in order and re-verifies its hash chain (ISO 15489 / 16363).</summary>
public sealed class AuditVerificationService(AppDbContext db) : IAuditVerificationService
{
    public async Task<AuditChainReport> VerifyChainAsync(CancellationToken ct = default)
    {
        // Loaded in full so the chain can be walked end-to-end; batch if the log grows very large.
        var entries = await db.AuditLogs.OrderBy(a => a.Id).ToListAsync(ct);
        var r = AuditChainVerifier.Verify(entries);
        return new AuditChainReport(r.Intact, r.Total, r.Verified, r.FirstBrokenId, r.Detail);
    }

    public async Task<int> ResealAsync(CancellationToken ct = default)
    {
        var entries = await db.AuditLogs.OrderBy(a => a.Id).ToListAsync(ct);
        string? previousHash = null;
        foreach (var e in entries)
        {
            e.PreviousHash = previousHash;
            e.Hash = AuditHash.Compute(e, previousHash);
            previousHash = e.Hash;
        }
        await db.SaveChangesAsync(ct);
        return entries.Count;
    }
}
