using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Users;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class UserAdminService(
    AppDbContext db,
    IPasswordHasher hasher,
    IAuditWriter audit,
    IAccessScope scope,
    ICurrentUser currentUser) : IUserAdminService
{
    private const string AdminRole = "System Administrator";

    /* ── Whose staff records this caller may touch ─────────────────────────────────
       The System Administrator manages the institution; everyone else manages their own
       department and nobody else's. A head of department who holds Users.View could otherwise
       read the whole institution's staff directory — every email, role and last-seen time — and
       one who holds Users.Edit could reset any password in the building. */

    private async Task<IQueryable<User>> ScopedAsync(IQueryable<User> q, CancellationToken ct)
    {
        if (await scope.IsSystemAdminAsync(ct)) return q;
        var me = currentUser.UserId;
        var depts = await scope.OrgUnitIdsAsync(ct);
        // No department means no department's worth of people — just yourself, not everyone.
        return depts.Count == 0
            ? q.Where(u => u.Id == me)
            : q.Where(u => (u.OrgUnitId != null && depts.Contains(u.OrgUnitId.Value)) || u.Id == me);
    }

    /// <summary>Whether this specific account is one the caller may act on, with the reason if not.</summary>
    private async Task<string?> DenyReasonAsync(User target, CancellationToken ct)
    {
        if (await scope.IsSystemAdminAsync(ct)) return null;
        var depts = await scope.OrgUnitIdsAsync(ct);
        if (target.Id != currentUser.UserId
            && (target.OrgUnitId is not { } targetDept || !depts.Contains(targetDept)))
            return "لا تملك صلاحية على مستخدم خارج قسمك";
        // An administrator's account is not a department's business even when they sit in one:
        // resetting its password would hand the whole institution over.
        if (await db.UserRoles.AnyAsync(
                ur => ur.UserId == target.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == AdminRole), ct))
            return "لا يمكن التعديل على حساب مدير النظام";
        return null;
    }

    /// <summary>The one role a department head may hand out.</summary>
    private const string StaffRole = "Employee";

    /// <summary>
    /// Whether this caller may assign these roles, with the reason if not.
    ///
    /// A head staffs their department with staff. Every other role reaches past the department, so
    /// handing one out is the administrator's decision:
    ///
    ///   • System Administrator is the whole institution, scope and all.
    ///   • Archive Officer carries <c>Archive.Archive</c> — the custody right, which makes every
    ///     document in the institution visible. A head who could grant it could mint an account
    ///     that sees far more than they do, then sign in as it. This was open: a head seeing 50
    ///     documents could create an archivist seeing 188.
    ///   • Manager carries the approval and oversight rights a department is answerable for.
    ///
    /// Restricting to one named role rather than computing "roles that grant nothing new" is
    /// deliberate: the computed version silently widens whenever somebody edits the permission
    /// matrix, and this is the boundary that must not move by accident.
    /// </summary>
    private async Task<string?> RoleGrantDenyReasonAsync(IReadOnlyCollection<long> roleIds, CancellationToken ct)
    {
        if (roleIds.Count == 0 || await scope.IsSystemAdminAsync(ct)) return null;
        var names = await db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).ToListAsync(ct);
        if (names.Any(n => n == AdminRole)) return "لا يمكن منح دور مدير النظام";
        return names.All(n => n == StaffRole)
            ? null
            : "يمكن لمدير القسم إسناد دور «موظف» فقط — الأدوار الأخرى يمنحها مدير النظام";
    }

    /// <summary>The Employee role, used when a scoped caller creates an account without naming one.</summary>
    private async Task<long?> StaffRoleIdAsync(CancellationToken ct) =>
        await db.Roles.Where(r => r.Name == StaffRole).Select(r => (long?)r.Id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AdminRoleDto>> ListRolesAsync(CancellationToken ct = default) =>
        await db.Roles.OrderBy(r => r.Name)
            .Select(r => new AdminRoleDto(r.Id, r.Name, r.Description))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AdminUserListItem>> ListUsersAsync(CancellationToken ct = default) =>
        await WithDepartmentsAsync(
            await (await ScopedAsync(db.Users, ct))
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .OrderBy(u => u.FullName)
                .ToListAsync(ct),
            ct);

    /// <summary>Puts a user in charge of departments beyond their own (requirement 5), replacing
    /// whatever they had.
    ///
    /// Administrator-only, and deliberately so: this is the one control that widens what a scoped
    /// account can reach, and a department head who could operate it would simply grant themselves
    /// the institution. Their own department is never stored here — it is already theirs, and
    /// duplicating it would leave a grant behind if they transferred out.</summary>
    public async Task<Result<AdminUserListItem>> SetDepartmentsAsync(
        long userId, SetUserDepartmentsRequest req, CancellationToken ct = default)
    {
        if (!await scope.IsSystemAdminAsync(ct))
            return Result<AdminUserListItem>.Fail("منح صلاحية أقسام إضافية للمسؤول العام فقط");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<AdminUserListItem>.Fail("المستخدم غير موجود");

        var wanted = req.OrgUnitIds.Where(id => id != user.OrgUnitId).Distinct().ToList();
        var known = await db.OrgUnits.Where(o => wanted.Contains(o.Id)).Select(o => o.Id).ToListAsync(ct);
        if (known.Count != wanted.Count) return Result<AdminUserListItem>.Fail("الوحدة التنظيمية غير موجودة");

        var current = await db.UserOrgUnitAccess.Where(g => g.UserId == userId).ToListAsync(ct);
        db.UserOrgUnitAccess.RemoveRange(current.Where(g => !known.Contains(g.OrgUnitId)));
        foreach (var unit in known.Where(id => current.All(g => g.OrgUnitId != id)))
            db.UserOrgUnitAccess.Add(new UserOrgUnitAccess
            {
                UserId = userId,
                OrgUnitId = unit,
                GrantedByUserId = currentUser.UserId,
                Note = req.Note?.Trim(),
            });

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DepartmentsUpdated", "User", userId, user.FullName, ct: ct);
        return Result<AdminUserListItem>.Ok(await LoadDtoAsync(userId, ct));
    }

    public async Task<Result<AdminUserListItem>> CreateUserAsync(CreateUserRequest r, CancellationToken ct = default)
    {
        var email = r.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(r.FullName)) return Result<AdminUserListItem>.Fail("الاسم مطلوب");
        if (string.IsNullOrWhiteSpace(email)) return Result<AdminUserListItem>.Fail("البريد الإلكتروني مطلوب");
        if (!PasswordPolicy.IsStrong(r.Password, out var pwError))
            return Result<AdminUserListItem>.Fail(pwError!);
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            return Result<AdminUserListItem>.Fail("البريد الإلكتروني مستخدم مسبقًا");
        if (r.OrgUnitId is { } ouId && !await db.OrgUnits.AnyAsync(o => o.Id == ouId, ct))
            return Result<AdminUserListItem>.Fail("الوحدة التنظيمية غير موجودة");

        // A scoped caller staffs their own department. Creating into another one — or into none,
        // which is visible to everybody's queries — is the administrator's job.
        var orgUnitId = r.OrgUnitId;
        var roleIds = r.RoleIds;
        if (!await scope.IsSystemAdminAsync(ct))
        {
            var myDepts = await scope.OrgUnitIdsAsync(ct);
            if (myDepts.Count == 0) return Result<AdminUserListItem>.Fail("لا يمكن إنشاء مستخدم دون قسم");
            if (orgUnitId is { } into && !myDepts.Contains(into))
                return Result<AdminUserListItem>.Fail("لا يمكن إنشاء مستخدم في قسم آخر");
            // Unstated department means the caller's own, never "none".
            orgUnitId ??= myDepts[0];
            if (await RoleGrantDenyReasonAsync(r.RoleIds, ct) is { } roleDenied)
                return Result<AdminUserListItem>.Fail(roleDenied);
            // Naming no role at all would create an account that can do nothing and reach nothing.
            // A head only ever adds staff, so that is what an unstated role means.
            if (roleIds.Count == 0 && await StaffRoleIdAsync(ct) is { } staffId) roleIds = [staffId];
        }

        var user = new User
        {
            FullName     = r.FullName.Trim(),
            FirstName    = r.FirstName?.Trim(),
            SecondName   = r.SecondName?.Trim(),
            ThirdName    = r.ThirdName?.Trim(),
            FamilyName   = r.FamilyName?.Trim(),
            Gender       = r.Gender,
            NationalId   = r.NationalId?.Trim(),
            Email        = email,
            JobTitle     = r.JobTitle?.Trim() ?? string.Empty,
            OrgUnitId    = orgUnitId,
            PasswordHash = hasher.Hash(r.Password),
            Clearance    = r.Clearance,
            IsActive     = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        await ApplyRolesAsync(user.Id, roleIds, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "User", user.Id, user.FullName, ct: ct);

        return Result<AdminUserListItem>.Ok(await LoadDtoAsync(user.Id, ct));
    }

    public async Task<Result<AdminUserListItem>> SetRolesAsync(long userId, SetUserRolesRequest r, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<AdminUserListItem>.Fail("المستخدم غير موجود");
        if (await DenyReasonAsync(user, ct) is { } denied) return Result<AdminUserListItem>.Fail(denied);
        // The same boundary as creation — otherwise a head creates an employee and promotes them
        // a second later, which is the same escape in two steps.
        if (await RoleGrantDenyReasonAsync(r.RoleIds, ct) is { } roleDenied)
            return Result<AdminUserListItem>.Fail(roleDenied);

        var existing = await db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync(ct);
        db.UserRoles.RemoveRange(existing);
        await ApplyRolesAsync(userId, r.RoleIds, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("RolesUpdated", "User", userId, user.FullName, ct: ct);

        return Result<AdminUserListItem>.Ok(await LoadDtoAsync(userId, ct));
    }

    public async Task<Result<AdminUserListItem>> SetActiveAsync(long userId, bool isActive, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<AdminUserListItem>.Fail("المستخدم غير موجود");
        if (await DenyReasonAsync(user, ct) is { } denied) return Result<AdminUserListItem>.Fail(denied);

        user.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(isActive ? "Activated" : "Deactivated", "User", userId, user.FullName, ct: ct);
        return Result<AdminUserListItem>.Ok(await LoadDtoAsync(userId, ct));
    }

    public async Task<Result<bool>> ResetPasswordAsync(long userId, string newPassword, CancellationToken ct = default)
    {
        if (!PasswordPolicy.IsStrong(newPassword, out var pwError))
            return Result<bool>.Fail(pwError!);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<bool>.Fail("المستخدم غير موجود");
        if (await DenyReasonAsync(user, ct) is { } denied) return Result<bool>.Fail(denied);

        user.PasswordHash = hasher.Hash(newPassword);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PasswordReset", "User", userId, user.FullName, ct: ct);
        return Result<bool>.Ok(true);
    }

    /* ── Permissions given to one person ───────────────────────────────────────────
       Roles are institution-wide vocabulary and a head cannot edit them, but they routinely need
       one person in their department to be able to do one extra thing. That is what these two are
       for: additive grants on a single account, inside the department the caller already manages. */

    /// <summary>The permissions a department head may never pass on, whatever they themselves hold.
    ///
    /// Each of these reaches past the department it would be granted in:
    ///   • <c>Archive.Archive</c> is custody — it makes every document in the institution visible.
    ///   • <c>Users.*</c> is staff administration; a head who could grant it could have somebody
    ///     else grant them anything, which is the same escape with one more step in it.
    ///   • <c>Settings.*</c> is the institution's configuration, not a department's.
    ///
    /// Named rather than computed, for the same reason <see cref="StaffRole"/> is: a rule derived
    /// from the permission matrix silently widens the day somebody edits the matrix.</summary>
    private static bool IsHeadGrantable(string code, string resource) =>
        code != "Archive.Archive" && resource is not ("Users" or "Settings");

    /// <summary>The codes this caller may hand out. The administrator may grant anything; everyone
    /// else may grant only what they hold themselves, minus the codes above — so no sequence of
    /// grants can produce an account stronger than the person who created it.</summary>
    private async Task<HashSet<string>> GrantableCodesAsync(CancellationToken ct)
    {
        var all = await db.Permissions.Select(p => new { p.Code, p.Resource }).ToListAsync(ct);
        if (await scope.IsSystemAdminAsync(ct))
            return all.Select(p => p.Code).ToHashSet(StringComparer.Ordinal);

        var mine = await MyPermissionCodesAsync(ct);
        return all.Where(p => mine.Contains(p.Code) && IsHeadGrantable(p.Code, p.Resource))
                  .Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Everything the caller can do, from their roles and their own personal grants.</summary>
    private async Task<HashSet<string>> MyPermissionCodesAsync(CancellationToken ct)
    {
        var me = currentUser.UserId;
        var fromRoles = await db.UserRoles.Where(ur => ur.UserId == me)
            .SelectMany(ur => db.RolePermissions.Where(rp => rp.RoleId == ur.RoleId)
                .Select(rp => rp.Permission.Code))
            .ToListAsync(ct);
        var granted = await db.UserPermissionGrants.Where(g => g.UserId == me)
            .Select(g => g.Permission.Code).ToListAsync(ct);
        return fromRoles.Concat(granted).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Codes a user's roles carry — the part of their access this screen cannot change.</summary>
    private async Task<List<string>> RoleCodesOfAsync(long userId, CancellationToken ct) =>
        await db.UserRoles.Where(ur => ur.UserId == userId)
            .SelectMany(ur => db.RolePermissions.Where(rp => rp.RoleId == ur.RoleId)
                .Select(rp => rp.Permission.Code))
            .Distinct().ToListAsync(ct);

    public async Task<Result<UserPermissionsDto>> GetUserPermissionsAsync(long userId, CancellationToken ct = default)
    {
        var user = await (await ScopedAsync(db.Users, ct)).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<UserPermissionsDto>.Fail("المستخدم غير موجود");
        return Result<UserPermissionsDto>.Ok(await LoadPermissionsDtoAsync(user, ct));
    }

    public async Task<Result<UserPermissionsDto>> SetUserPermissionsAsync(
        long userId, SetUserPermissionsRequest req, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<UserPermissionsDto>.Fail("المستخدم غير موجود");
        // Same gate as changing their role or resetting their password: own department, and never
        // an administrator's account.
        if (await DenyReasonAsync(user, ct) is { } denied) return Result<UserPermissionsDto>.Fail(denied);

        var grantable = await GrantableCodesAsync(ct);
        var roleCodes = (await RoleCodesOfAsync(userId, ct)).ToHashSet(StringComparer.Ordinal);
        // What the role already gives is not this screen's to grant, so asking for it is a no-op
        // rather than an error — the checkbox is ticked and disabled, and a form round-trip
        // shouldn't fail on it.
        var wanted = req.PermissionCodes.Where(c => !roleCodes.Contains(c)).Distinct().ToList();

        /* Resolved from the whole catalogue in memory rather than with a `wanted.Contains(p.Code)`
           filter: MySQL refuses to translate a parameterised string-list Contains here ("Expression
           '@wanted' in the SQL tree does not have a type mapping assigned"), and the permission
           table is a few dozen rows — this is one small read either way. */
        var byCode = (await db.Permissions.Select(p => new { p.Code, p.Id }).ToListAsync(ct))
            .Where(p => wanted.Contains(p.Code))
            .ToDictionary(p => p.Code, p => p.Id, StringComparer.Ordinal);
        if (byCode.Count != wanted.Count) return Result<UserPermissionsDto>.Fail("صلاحية غير معروفة");

        var current = await db.UserPermissionGrants.Include(g => g.Permission)
            .Where(g => g.UserId == userId).ToListAsync(ct);

        /* The ceiling applies to what this save *changes*, not to what it leaves alone.
           A head who sends back a form listing an administrator's grant alongside their own is
           keeping that grant, not making it — refusing there would make the screen unusable for
           any employee the administrator had ever granted something. Adding and removing are both
           real changes, so both are held to what the caller could have granted themselves. */
        var added = wanted.Where(c => current.All(g => g.Permission.Code != c)).ToList();
        if (added.FirstOrDefault(c => !grantable.Contains(c)) is { } refused)
            return Result<UserPermissionsDto>.Fail($"لا يمكنك منح صلاحية لا تملكها: {refused}");

        var removed = current.Where(g => !wanted.Contains(g.Permission.Code)).ToList();
        if (removed.FirstOrDefault(g => !grantable.Contains(g.Permission.Code)) is { } locked)
            return Result<UserPermissionsDto>.Fail($"لا يمكنك سحب صلاحية لا تملكها: {locked.Permission.Code}");
        db.UserPermissionGrants.RemoveRange(removed);

        foreach (var code in added)
            db.UserPermissionGrants.Add(new UserPermissionGrant
            {
                UserId = userId,
                PermissionId = byCode[code],
                GrantedByUserId = currentUser.UserId,
                Note = req.Note?.Trim(),
            });

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PermissionsUpdated", "User", userId, user.FullName,
            newValues: string.Join(", ", wanted), ct: ct);
        return Result<UserPermissionsDto>.Ok(await LoadPermissionsDtoAsync(user, ct));
    }

    private async Task<UserPermissionsDto> LoadPermissionsDtoAsync(User user, CancellationToken ct)
    {
        var roleCodes = await RoleCodesOfAsync(user.Id, ct);
        var extra = await db.UserPermissionGrants.Where(g => g.UserId == user.Id)
            .Select(g => g.Permission.Code).ToListAsync(ct);
        var grantable = await GrantableCodesAsync(ct);
        return new UserPermissionsDto(user.Id, user.FullName,
            roleCodes.OrderBy(c => c, StringComparer.Ordinal).ToList(),
            extra.OrderBy(c => c, StringComparer.Ordinal).ToList(),
            grantable.OrderBy(c => c, StringComparer.Ordinal).ToList());
    }

    /* ── Sidebar sections hidden for one person ────────────────────────────────────
       Hiding is presentation, not access: the permission is what decides whether the API answers.
       A head tidying an employee's rail down to the work they actually do is doing housekeeping,
       which is why this needs no ceiling like the permission grants do — the worst outcome is a
       section someone has to reach by URL. */

    /// <summary>The one section that can never be hidden. It is the way back to every screen that
    /// could restore the others, so hiding it strands the person who holds it.</summary>
    private const string UnhideableSection = "settings";

    private static string[] ParseKeys(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public async Task<Result<UserNavSectionsDto>> GetUserNavSectionsAsync(long userId, CancellationToken ct = default)
    {
        var user = await (await ScopedAsync(db.Users, ct)).FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<UserNavSectionsDto>.Fail("المستخدم غير موجود");
        return Result<UserNavSectionsDto>.Ok(await ToNavDtoAsync(user, ct));
    }

    public async Task<Result<UserNavSectionsDto>> SetUserNavSectionsAsync(
        long userId, SetUserNavSectionsRequest req, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<UserNavSectionsDto>.Fail("المستخدم غير موجود");
        // Same gate as every other act on a staff record: own department, never an administrator.
        if (await DenyReasonAsync(user, ct) is { } denied) return Result<UserNavSectionsDto>.Fail(denied);

        var hidden = (req.Hidden ?? [])
            .Select(k => k.Trim())
            .Where(k => k.Length > 0 && k != UnhideableSection)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        user.HiddenNavKeys = hidden.Length == 0 ? null : string.Join(',', hidden);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("NavSectionsUpdated", "User", userId, user.FullName,
            newValues: user.HiddenNavKeys, ct: ct);
        return Result<UserNavSectionsDto>.Ok(await ToNavDtoAsync(user, ct));
    }

    private async Task<UserNavSectionsDto> ToNavDtoAsync(User user, CancellationToken ct)
    {
        var orgHidden = await db.Institutions.OrderBy(i => i.Id)
            .Select(i => i.HiddenNavKeys).FirstOrDefaultAsync(ct);
        return new UserNavSectionsDto(user.Id, user.FullName,
            ParseKeys(user.HiddenNavKeys), ParseKeys(orgHidden));
    }

    // ---- role management ----

    public async Task<IReadOnlyList<PermissionInfoDto>> ListPermissionsAsync(CancellationToken ct = default) =>
        await db.Permissions.OrderBy(p => p.Resource).ThenBy(p => p.Action)
            .Select(p => new PermissionInfoDto(p.Code, p.Resource, p.Action.ToString()))
            .ToListAsync(ct);

    public async Task<Result<RolePermissionsDto>> GetRolePermissionsAsync(long roleId, CancellationToken ct = default)
    {
        var role = await db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null) return Result<RolePermissionsDto>.Fail("الدور غير موجود");
        var codes = role.RolePermissions.Select(rp => rp.Permission.Code).ToList();
        return Result<RolePermissionsDto>.Ok(new RolePermissionsDto(role.Id, role.Name, role.Description, role.IsSystem, codes));
    }

    public async Task<Result<RolePermissionsDto>> SetRolePermissionsAsync(long roleId, SetRolePermissionsRequest req, CancellationToken ct = default)
    {
        // Roles and their permissions are institution-wide: a department head editing them would
        // be changing what every other department can do.
        if (!await scope.IsSystemAdminAsync(ct)) return Result<RolePermissionsDto>.Fail("إدارة الأدوار للمسؤول العام فقط");
        var role = await db.Roles.Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null) return Result<RolePermissionsDto>.Fail("الدور غير موجود");

        // Remove all current permissions then re-add the requested ones.
        db.RolePermissions.RemoveRange(role.RolePermissions);

        var codeSet = req.PermissionCodes.ToHashSet();
        var allPerms = (await db.Permissions.ToListAsync(ct)).Where(p => codeSet.Contains(p.Code));
        foreach (var perm in allPerms)
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = perm.Id });

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PermissionsUpdated", "Role", roleId, role.Name, ct: ct);

        return await GetRolePermissionsAsync(roleId, ct);
    }

    public async Task<Result<RolePermissionsDto>> ResetRolePermissionsAsync(long roleId, CancellationToken ct = default)
    {
        // Roles and their permissions are institution-wide: a department head editing them would
        // be changing what every other department can do.
        if (!await scope.IsSystemAdminAsync(ct)) return Result<RolePermissionsDto>.Fail("إدارة الأدوار للمسؤول العام فقط");
        var role = await db.Roles.Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null) return Result<RolePermissionsDto>.Fail("الدور غير موجود");
        if (!role.IsSystem) return Result<RolePermissionsDto>.Fail("إعادة التعيين الافتراضي متاحة للأدوار المدمجة فقط");

        // The defaults live in PermissionCatalog alongside the seeder's, because two hand-kept copies
        // had already drifted: this one still granted the retired "Reports" resource and knew nothing
        // about Disposition, Destruction, LegalHold or Correspondence.
        var pick = PermissionCatalog.RoleDefaults
            .FirstOrDefault(r => r.Name == role.Name).Pick ?? (_ => false);

        var allPerms = await db.Permissions.ToListAsync(ct);
        db.RolePermissions.RemoveRange(role.RolePermissions);
        foreach (var perm in allPerms.Where(pick))
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = perm.Id });

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PermissionsReset", "Role", roleId, role.Name, ct: ct);
        return await GetRolePermissionsAsync(roleId, ct);
    }

    public async Task<Result<AdminRoleDto>> CreateRoleAsync(CreateRoleRequest req, CancellationToken ct = default)
    {
        // Roles and their permissions are institution-wide: a department head editing them would
        // be changing what every other department can do.
        if (!await scope.IsSystemAdminAsync(ct)) return Result<AdminRoleDto>.Fail("إدارة الأدوار للمسؤول العام فقط");
        if (string.IsNullOrWhiteSpace(req.Name))
            return Result<AdminRoleDto>.Fail("اسم الدور مطلوب");
        if (await db.Roles.AnyAsync(r => r.Name == req.Name.Trim(), ct))
            return Result<AdminRoleDto>.Fail("يوجد دور بهذا الاسم مسبقًا");

        var role = new Role { Name = req.Name.Trim(), Description = req.Description?.Trim(), IsSystem = false };
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "Role", role.Id, role.Name, ct: ct);
        return Result<AdminRoleDto>.Ok(new AdminRoleDto(role.Id, role.Name, role.Description));
    }

    public async Task<Result<bool>> DeleteRoleAsync(long roleId, CancellationToken ct = default)
    {
        // Roles and their permissions are institution-wide: a department head editing them would
        // be changing what every other department can do.
        if (!await scope.IsSystemAdminAsync(ct)) return Result<bool>.Fail("إدارة الأدوار للمسؤول العام فقط");
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null) return Result<bool>.Fail("الدور غير موجود");
        if (role.IsSystem) return Result<bool>.Fail("لا يمكن حذف الأدوار المدمجة في النظام");
        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == roleId, ct))
            return Result<bool>.Fail("لا يمكن حذف دور مُعيَّن لمستخدمين. أزل المستخدمين من الدور أولًا.");

        var rolePerms = await db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        db.RolePermissions.RemoveRange(rolePerms);
        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "Role", roleId, role.Name, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ---- helpers ----

    private async Task ApplyRolesAsync(long userId, IReadOnlyList<long> roleIds, CancellationToken ct)
    {
        if (roleIds is null || roleIds.Count == 0) return;
        var validIds = await db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Id).ToListAsync(ct);
        foreach (var rid in validIds.Distinct())
            db.UserRoles.Add(new UserRole { UserId = userId, RoleId = rid });
    }

    private async Task<AdminUserListItem> LoadDtoAsync(long userId, CancellationToken ct)
    {
        var user = await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstAsync(u => u.Id == userId, ct);
        return (await WithDepartmentsAsync([user], ct))[0];
    }

    /// <summary>Maps users to DTOs, attaching each one's departments. Names and grants are fetched
    /// once for the whole page rather than per row.</summary>
    private async Task<List<AdminUserListItem>> WithDepartmentsAsync(
        IReadOnlyList<User> users, CancellationToken ct)
    {
        var ids = users.Select(u => u.Id).ToList();
        var grants = await db.UserOrgUnitAccess.Where(g => ids.Contains(g.UserId))
            .Select(g => new { g.UserId, g.OrgUnitId }).ToListAsync(ct);

        var unitIds = grants.Select(g => g.OrgUnitId)
            .Concat(users.Where(u => u.OrgUnitId is not null).Select(u => u.OrgUnitId!.Value))
            .Distinct().ToList();
        var names = unitIds.Count == 0
            ? []
            : await db.OrgUnits.Where(o => unitIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name, ct);

        return users.Select(u =>
        {
            var extra = grants.Where(g => g.UserId == u.Id).Select(g => g.OrgUnitId).ToList();
            return ToDto(u) with
            {
                OrgUnitId = u.OrgUnitId,
                OrgUnitName = u.OrgUnitId is { } own ? names.GetValueOrDefault(own) : null,
                ExtraOrgUnitIds = extra,
                ExtraOrgUnitNames = extra.Select(id => names.GetValueOrDefault(id, "—")).ToList(),
            };
        }).ToList();
    }

    private static AdminUserListItem ToDto(User u) => new(
        u.Id, u.FullName,
        u.FirstName, u.SecondName, u.ThirdName, u.FamilyName,
        u.Gender.ToString(), u.NationalId,
        u.Email, u.JobTitle, u.Clearance.ToString(), u.IsActive,
        u.UserRoles.Select(ur => ur.Role.Name).OrderBy(n => n).ToList(),
        u.UserRoles.Select(ur => ur.RoleId).ToList());
}
