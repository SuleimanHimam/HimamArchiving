using Archiving.Application.Common.Interfaces;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <inheritdoc cref="IAccessScope"/>
public sealed class AccessScope(AppDbContext db, ICurrentUser currentUser) : IAccessScope
{
    // Per-request cache. The scope is consulted on every list query in a request, and the answers
    // cannot change mid-request: roles and department are read from the same connection.
    private bool? _isAdmin;
    private long? _orgUnitId;
    private bool _orgUnitLoaded;
    private IReadOnlyList<long>? _orgUnitIds;
    private IReadOnlyList<long>? _managedOrgUnitIds;
    private readonly Dictionary<string, bool> _permissions = [];

    public async Task<bool> IsSystemAdminAsync(CancellationToken ct = default)
    {
        if (_isAdmin is { } cached) return cached;
        var me = currentUser.UserId;
        _isAdmin = me is not null && await db.UserRoles.AnyAsync(
            ur => ur.UserId == me && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "System Administrator"), ct);
        return _isAdmin.Value;
    }

    public async Task<bool> HasPermissionAsync(string code, CancellationToken ct = default)
    {
        if (_permissions.TryGetValue(code, out var cached)) return cached;
        var me = currentUser.UserId;
        var has = me is not null && (await IsSystemAdminAsync(ct)
            || await db.UserRoles.AnyAsync(
                ur => ur.UserId == me && db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId
                    && db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == code)), ct)
            // Personal grants count here too, or a granted right would pass the endpoint gate (which
            // reads the token) and then be refused by the service that re-checks against the database.
            || await db.UserPermissionGrants.AnyAsync(
                g => g.UserId == me && db.Permissions.Any(p => p.Id == g.PermissionId && p.Code == code), ct));
        _permissions[code] = has;
        return has;
    }

    public async Task<long?> OrgUnitIdAsync(CancellationToken ct = default)
    {
        if (_orgUnitLoaded) return _orgUnitId;
        var me = currentUser.UserId;
        _orgUnitId = me is null
            ? null
            : await db.Users.Where(u => u.Id == me).Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct);
        _orgUnitLoaded = true;
        return _orgUnitId;
    }

    public async Task<IReadOnlyList<long>> OrgUnitIdsAsync(CancellationToken ct = default)
    {
        if (_orgUnitIds is { } cached) return cached;
        var me = currentUser.UserId;
        if (me is null) return _orgUnitIds = [];

        var granted = await db.UserOrgUnitAccess.Where(g => g.UserId == me)
            .Select(g => g.OrgUnitId).ToListAsync(ct);
        // The home department leads: it is the one a new record is filed under, and callers that
        // want a single unit take the first.
        if (await OrgUnitIdAsync(ct) is { } home) granted.Insert(0, home);
        return _orgUnitIds = granted.Distinct().ToList();
    }

    public async Task<IReadOnlyList<long>> ManagedOrgUnitIdsAsync(CancellationToken ct = default)
    {
        if (_managedOrgUnitIds is { } cached) return cached;
        var me = currentUser.UserId;
        if (me is null || await IsSystemAdminAsync(ct)) return _managedOrgUnitIds = [];

        // Granted departments are managed by definition — the grant is what puts the person in
        // charge of a unit that is not their own.
        var managed = await db.UserOrgUnitAccess.Where(g => g.UserId == me)
            .Select(g => g.OrgUnitId).ToListAsync(ct);

        /* Their own department is managed only if they actually head it. Two ways to hold that
           seat, and both count: occupying the unit's manager position, or being assigned to it —
           the assignment history is what an acting appointment is recorded as, and a unit whose
           `CurrentOccupantUserId` was never filled in would otherwise have no head at all. */
        if (await OrgUnitIdAsync(ct) is { } home
            && await db.OrgUnits.AnyAsync(o => o.Id == home && o.ManagerPositionId != null
                && db.Positions.Any(p => p.Id == o.ManagerPositionId
                    && (p.CurrentOccupantUserId == me
                        || db.PositionAssignments.Any(a => a.PositionId == p.Id && a.UserId == me && a.IsCurrent))), ct))
            managed.Add(home);

        return _managedOrgUnitIds = managed.Distinct().ToList();
    }

    public async Task<bool> SeesAllDocumentsAsync(CancellationToken ct = default) =>
        await HasPermissionAsync("Archive.Archive", ct);
}
