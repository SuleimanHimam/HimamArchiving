using System.Linq.Expressions;
using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

public sealed record AuditLogDto(
    long Id,
    long? UserId,
    string? UserEmail,
    string? UserFullName,
    string Action,
    string? EntityType,
    long? EntityId,
    string? EntityTitle,
    string? IpAddress,
    string? MachineName,
    string? OldValues,
    string? NewValues,
    DateTime CreatedAt);

/// <summary>Audit-trail read + integrity verification — ISO 15489 / 16363.</summary>
[ApiController]
[Route("api/audit")]
[Authorize]
public sealed class AuditController(
    IAuditVerificationService service, AppDbContext db, IAuditWriter audit,
    IAccessScope scope, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Narrows the log to the departments the caller heads: entries written by the people
    /// in them, plus the caller's own.
    ///
    /// A department head answers for their own staff, not for the institution — so a manager reads
    /// their department's activity and nothing else, and the same narrowing decides what they may
    /// hide. An employee reads only what they themselves did: their colleagues' activity is not
    /// theirs to monitor, even inside their own department. Only the System Administrator reads
    /// the whole log.
    ///
    /// Entries with no user are the system's own (background jobs, retention sweeps). They belong
    /// to no department, so they stay out of a departmental view rather than appearing in every one.</summary>
    private async Task<IQueryable<AuditLog>> ScopedAsync(IQueryable<AuditLog> q, CancellationToken ct)
    {
        if (await scope.IsSystemAdminAsync(ct)) return q;
        var me = currentUser.UserId;
        var managed = await scope.ManagedOrgUnitIdsAsync(ct);
        // Heading no department means no departmental log — their own entries only, never everyone's.
        if (managed.Count == 0) return q.Where(a => a.UserId == me);
        return q.Where(a => a.UserId == me
            || (a.UserId != null && db.Users.Any(u => u.Id == a.UserId
                && u.OrgUnitId != null && managed.Contains(u.OrgUnitId.Value))));
    }

    /// <summary>Paginated, filterable audit log.</summary>
    [HttpGet("logs")]
    [HasPermission("Audit.View")]
    public async Task<IActionResult> Logs(
        [FromQuery] string?  entityType,
        [FromQuery] string?  action,
        [FromQuery] long?    userId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page     = Math.Max(1, page);

        var q = await ScopedAsync(db.AuditLogs.AsNoTracking().Where(a => !a.IsHidden), ct);

        if (!string.IsNullOrWhiteSpace(entityType))
            q = q.Where(a => a.EntityType == entityType);

        // Comma-separated, because one action can be written under several codes: the filter
        // derives Create/Edit/Delete/View from the HTTP verb while services write Created/
        // Updated/Deleted/Viewed. They are one action to the reader, so one filter chip sends
        // every spelling of it.
        if (!string.IsNullOrWhiteSpace(action))
        {
            var codes = action.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            q = codes.Length == 1 ? q.Where(a => a.Action == codes[0]) : q.Where(ActionIn(codes));
        }

        if (userId.HasValue)
            q = q.Where(a => a.UserId == userId.Value);

        if (from.HasValue)
            q = q.Where(a => a.CreatedAt >= from.Value.ToUniversalTime());

        if (to.HasValue)
            q = q.Where(a => a.CreatedAt <= to.Value.ToUniversalTime());

        var total = await q.CountAsync(ct);

        // Load the page, then join user info in memory (AuditLog has no FK nav property).
        var rows = await q
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Collect user IDs needed for this page only.
        var userIds = rows.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var users = userIds.Count == 0
            ? []
            : await db.Users.IgnoreQueryFilters()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Email, u.FullName })
                .ToDictionaryAsync(u => u.Id, ct);

        var dtos = rows.Select(r =>
        {
            users.TryGetValue(r.UserId ?? 0, out var u);
            return new AuditLogDto(
                r.Id, r.UserId,
                u?.Email, u?.FullName,
                r.Action, r.EntityType, r.EntityId, r.EntityTitle,
                r.IpAddress, r.MachineName,
                r.OldValues, r.NewValues,
                r.CreatedAt);
        }).ToList();

        return Ok(new { total, page, pageSize, items = dtos });
    }

    /// <summary>`Action` equals any of <paramref name="codes"/>, as an OR chain.
    /// Built by hand because the MySQL provider cannot type-map a parameterised collection:
    /// a plain <c>codes.Contains(a.Action)</c> throws "Expression '@codes' … does not have a
    /// type mapping assigned" at translation time.</summary>
    private static Expression<Func<AuditLog, bool>> ActionIn(string[] codes)
    {
        var row  = Expression.Parameter(typeof(AuditLog), "a");
        var prop = Expression.Property(row, nameof(AuditLog.Action));
        var body = codes
            .Select(c => (Expression)Expression.Equal(prop, Expression.Constant(c)))
            .Aggregate(Expression.OrElse);
        return Expression.Lambda<Func<AuditLog, bool>>(body, row);
    }

    /// <summary>Distinct users who have entries in the audit log — used to populate filter dropdowns.</summary>
    [HttpGet("users")]
    [HasPermission("Audit.View")]
    public async Task<IActionResult> AuditUsers(CancellationToken ct)
    {
        var ids = await (await ScopedAsync(db.AuditLogs, ct))
            .Where(a => a.UserId != null)
            .Select(a => a.UserId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var result = await db.Users.IgnoreQueryFilters()
            .Where(u => ids.Contains(u.Id))
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToListAsync(ct);

        return Ok(result);
    }

    /// <summary>Distinct entity types present in the audit log, for filter dropdowns.</summary>
    [HttpGet("entity-types")]
    [HasPermission("Audit.View")]
    public async Task<IActionResult> EntityTypes(CancellationToken ct)
    {
        var types = await (await ScopedAsync(db.AuditLogs, ct))
            .Select(a => a.EntityType)
            .Distinct()
            .Where(t => t != null)
            .OrderBy(t => t)
            .ToListAsync(ct);
        return Ok(types);
    }

    /// <summary>Distinct actions present in the audit log, for filter dropdowns.
    /// Taken from the data rather than a fixed list: entries arrive from two sources with different
    /// vocabularies — AuditActionFilter derives Create/Edit/Delete/View from the HTTP verb, while
    /// services write past-tense semantic actions (Created, Archived, DispositionRejected, …).
    /// A hardcoded UI list matched neither and offered filters that returned nothing.</summary>
    [HttpGet("actions")]
    [HasPermission("Audit.View")]
    public async Task<IActionResult> Actions(CancellationToken ct)
    {
        var actions = await (await ScopedAsync(db.AuditLogs, ct))
            .Where(a => !a.IsHidden)
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .ToListAsync(ct);
        return Ok(actions);
    }

    /// <summary>Verifies the audit hash chain end-to-end; reports the first broken entry if any.</summary>
    [HttpGet("verify")]
    [HasPermission("Audit.View")]
    public async Task<IActionResult> Verify(CancellationToken ct) => Ok(await service.VerifyChainAsync(ct));

    /// <summary>One-time baseline reseal of the audit chain (e.g. after a hashing-scheme correction).
    /// Admin-gated and itself recorded in the audit log.</summary>
    [HttpPost("reseal")]
    [HasPermission("Audit.Edit")]
    public async Task<IActionResult> Reseal(CancellationToken ct)
        => Ok(new { resealed = await service.ResealAsync(ct) });

    /// <summary>Soft-hides a log entry from the monitoring view. The row is retained physically so the
    /// tamper-evident hash chain stays intact (verification still passes); it just no longer appears.</summary>
    [HttpDelete("logs/{id:long}")]
    [HasPermission("Audit.Delete")]
    public async Task<IActionResult> HideLog(long id, CancellationToken ct)
    {
        // Hiding follows reading: whatever the caller cannot see, they cannot hide either.
        var row = await (await ScopedAsync(db.AuditLogs, ct)).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row is null) return NotFound();
        row.IsHidden = true;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Soft-hides ALL currently-visible log entries from the monitoring view in one action.
    /// Rows are retained physically, so the tamper-evident hash chain stays intact (verify still passes).</summary>
    [HttpDelete("logs")]
    [HasPermission("Audit.Delete")]
    public async Task<IActionResult> HideAllLogs(CancellationToken ct)
    {
        var hidden = await (await ScopedAsync(db.AuditLogs, ct))
            .Where(a => !a.IsHidden)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsHidden, true), ct);
        // Record who cleared the log — entityType "AuditLog" (not the suppressed "Audit") so it is kept,
        // appearing as the first/only motion afterwards.
        await audit.WriteAsync("Delete", "AuditLog", 0, "تم حذف جميع سجلات الحركات", ct: ct);
        return Ok(new { hidden });
    }
}
