using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
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
public sealed class AuditController(IAuditVerificationService service, AppDbContext db) : ControllerBase
{
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

        var q = db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(entityType))
            q = q.Where(a => a.EntityType == entityType);

        if (!string.IsNullOrWhiteSpace(action))
            q = q.Where(a => a.Action == action);

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

    /// <summary>Distinct users who have entries in the audit log — used to populate filter dropdowns.</summary>
    [HttpGet("users")]
    [HasPermission("Audit.View")]
    public async Task<IActionResult> AuditUsers(CancellationToken ct)
    {
        var ids = await db.AuditLogs
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
        var types = await db.AuditLogs
            .Select(a => a.EntityType)
            .Distinct()
            .Where(t => t != null)
            .OrderBy(t => t)
            .ToListAsync(ct);
        return Ok(types);
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
}
