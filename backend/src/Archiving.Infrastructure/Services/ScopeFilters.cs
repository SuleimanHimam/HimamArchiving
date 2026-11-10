using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>
/// The visibility predicates, in one place.
///
/// A register and the dashboard that counts it have to agree: a chart that totals letters the
/// caller cannot open is not a summary of their work, it is a leak with a nicer presentation —
/// it tells a department head how much correspondence another department handled this month, and
/// an employee how much their colleagues did. The two had drifted exactly that way, because the
/// register filtered by department while the dashboard filtered by clearance alone.
///
/// Keeping the rule as one expression is what stops them drifting again.
/// </summary>
public static class ScopeFilters
{
    /// <summary>
    /// Register entries the caller has to do with: the ones they registered, the ones they were
    /// let in on, and — for a department head — everything addressed to a department they head or
    /// registered by their own staff. Mirrors <c>DiwanService.ScopedAsync</c>.
    ///
    /// Routing addresses a letter to a department, not to its whole staff. Membership alone used to
    /// open it, which handed every employee the department's external correspondence wholesale —
    /// its complaints, its claims, its letters about them. The head sees what arrives and names who
    /// handles it (<see cref="DiwanAccess"/>); an employee otherwise sees only their own.
    /// </summary>
    public static IQueryable<DiwanMail> VisibleDiwan(
        this IQueryable<DiwanMail> q, AppDbContext db, long? me, IReadOnlyList<long> managed) =>
        q.Where(x => x.CreatedBy == me
            || db.DiwanAccess.Any(a => a.DiwanMailId == x.Id && a.UserId == me)
            || db.DiwanBureaus.Any(bu => bu.DiwanMailId == x.Id && managed.Contains(bu.OrgUnitId))
            || db.Users.Any(u => u.Id == x.CreatedBy && u.OrgUnitId != null && managed.Contains(u.OrgUnitId.Value)));

    /// <summary>Documents the caller may list: their own, their departments', and anything shared
    /// with them personally. Mirrors <c>DocumentService.ListAsync</c>.</summary>
    public static IQueryable<Document> VisibleDocuments(
        this IQueryable<Document> q, AppDbContext db, long? me, IReadOnlyList<long> depts) =>
        q.Where(d => d.CreatedBy == me
            || depts.Contains(d.OwningOrgUnitId)
            || db.DocumentShares.Any(sh => sh.DocumentId == d.Id && sh.SharedWithUserId == me));

    /// <summary>The name the archive department is seeded under. Renaming the unit breaks nothing
    /// else, so the lookup falls back to the department of whoever holds the custody right.</summary>
    private const string ArchiveUnitName = "قسم الأرشيف";

    /// <summary>
    /// Disposal requests the caller may see. مدير قسم الأرشيف ratifies every destruction in the
    /// institution and the archive custodians carry them out, so both see all of them; a
    /// department head sees what their own departments raised, because that is what they approve;
    /// everyone else sees only their own requests.
    ///
    /// Shared with the dashboard tile that counts them. The tile used to gate on the permission
    /// alone, so a head holding Disposition.Approve was told there were 139 requests waiting while
    /// the screen the tile links to showed them none — the count was the institution's and the
    /// list was theirs.
    /// </summary>
    public static async Task<IQueryable<DispositionRequest>> VisibleDispositionsAsync(
        this IQueryable<DispositionRequest> q, AppDbContext db, IAccessScope scope, long? me,
        CancellationToken ct)
    {
        if (await scope.IsSystemAdminAsync(ct)
            || await scope.HasPermissionAsync("Archive.Archive", ct)
            || await ArchiveManagerAsync(db, ct) == me) return q;

        var depts = await scope.OrgUnitIdsAsync(ct);
        if (await scope.HasPermissionAsync("Disposition.Approve", ct) && depts.Count > 0)
            return q.Where(r => r.RequestedByUserId == me
                || db.Users.Any(u => u.Id == r.RequestedByUserId
                    && u.OrgUnitId != null && depts.Contains(u.OrgUnitId.Value)));

        return q.Where(r => r.RequestedByUserId == me);
    }

    /// <summary>مدير قسم الأرشيف — the seat that ratifies every destruction, whichever department
    /// asked for it.</summary>
    public static async Task<long?> ArchiveManagerAsync(AppDbContext db, CancellationToken ct)
    {
        var unitId = await db.OrgUnits.Where(o => o.Name == ArchiveUnitName)
            .Select(o => (long?)o.Id).FirstOrDefaultAsync(ct)
            ?? await db.Users
                .Where(u => u.IsActive && u.OrgUnitId != null && db.UserRoles.Any(ur => ur.UserId == u.Id
                    && db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId
                        && db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == "Archive.Archive"))))
                .Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct);
        if (unitId is not { } uid) return null;

        var seat = await db.OrgUnits.Where(o => o.Id == uid).Select(o => o.ManagerPositionId).FirstOrDefaultAsync(ct);
        return seat is not { } pid
            ? null
            : await db.Positions.Where(p => p.Id == pid).Select(p => p.CurrentOccupantUserId).FirstOrDefaultAsync(ct);
    }

    /// <summary>Workflow tasks the caller is answerable for: assigned to them personally, or to a
    /// seat in a department they act in. Without the second half a head sees nothing of their own
    /// department's queue; without the first, holding Workflow.View meant seeing the institution's.</summary>
    public static IQueryable<WorkflowTask> VisibleTasks(
        this IQueryable<WorkflowTask> q, AppDbContext db, long? me, IReadOnlyList<long> depts) =>
        q.Where(t => t.AssignedToUserId == me
            || (t.AssignedToPositionId != null
                && db.Positions.Any(p => p.Id == t.AssignedToPositionId && depts.Contains(p.OrgUnitId))));

    /// <summary>Resolves the three lists every predicate above needs, in one pass.</summary>
    public static async Task<(bool IsAdmin, IReadOnlyList<long> Depts, IReadOnlyList<long> Managed)>
        ResolveAsync(this IAccessScope scope, CancellationToken ct) =>
        (await scope.IsSystemAdminAsync(ct),
         await scope.OrgUnitIdsAsync(ct),
         await scope.ManagedOrgUnitIdsAsync(ct));
}
