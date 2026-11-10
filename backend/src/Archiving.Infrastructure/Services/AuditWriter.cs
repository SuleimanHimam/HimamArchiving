using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>Appends hash-chained audit entries: each row's hash covers its content plus the prior row's hash,
/// so any tampering breaks the chain.</summary>
public sealed class AuditWriter(AppDbContext db, ICurrentUser currentUser) : IAuditWriter
{
    // Only meaningful record actions are journaled into the monitoring log: Create / Edit / Delete /
    // Download / Print. Navigation/View, Approve, Forward, Archive, Login/Logout, etc. are intentionally
    // not recorded. Audit-on-audit (e.g. hiding a log entry) is never recorded either.
    private static readonly string[] JournaledStems = { "create", "edit", "delete", "download", "print" };

    private static bool IsJournaled(string action) =>
        Array.Exists(JournaledStems, s => action.StartsWith(s, StringComparison.OrdinalIgnoreCase));

    public async Task WriteAsync(
        string action, string entityType, long entityId,
        string? entityTitle = null, string? oldValues = null, string? newValues = null,
        CancellationToken ct = default)
    {
        if (string.Equals(entityType, "Audit", StringComparison.OrdinalIgnoreCase) || !IsJournaled(action))
            return;

        var previousHash = await db.AuditLogs
            .OrderByDescending(a => a.Id)
            .Select(a => a.Hash)
            .FirstOrDefaultAsync(ct);

        var entry = new AuditLog
        {
            UserId = currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityTitle = entityTitle,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = currentUser.IpAddress,
            UserAgent = currentUser.UserAgent,
            MachineName = Environment.MachineName,
            CreatedAt = AuditHash.Truncate(DateTime.UtcNow), // microsecond precision: hash stays stable on round-trip
            PreviousHash = previousHash,
        };
        entry.Hash = AuditHash.Compute(entry, previousHash);

        db.AuditLogs.Add(entry);
        await db.SaveChangesAsync(ct);
    }
}
