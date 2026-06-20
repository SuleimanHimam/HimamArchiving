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
    IAuditWriter audit) : IUserAdminService
{
    public async Task<IReadOnlyList<AdminRoleDto>> ListRolesAsync(CancellationToken ct = default) =>
        await db.Roles.OrderBy(r => r.Name)
            .Select(r => new AdminRoleDto(r.Id, r.Name, r.Description))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AdminUserListItem>> ListUsersAsync(CancellationToken ct = default) =>
        await db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.FullName)
            .Select(u => ToDto(u))
            .ToListAsync(ct);

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
            OrgUnitId    = r.OrgUnitId,
            PasswordHash = hasher.Hash(r.Password),
            Clearance    = r.Clearance,
            IsActive     = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        await ApplyRolesAsync(user.Id, r.RoleIds, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "User", user.Id, user.FullName, ct: ct);

        return Result<AdminUserListItem>.Ok(await LoadDtoAsync(user.Id, ct));
    }

    public async Task<Result<AdminUserListItem>> SetRolesAsync(long userId, SetUserRolesRequest r, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<AdminUserListItem>.Fail("المستخدم غير موجود");

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

        user.PasswordHash = hasher.Hash(newPassword);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PasswordReset", "User", userId, user.FullName, ct: ct);
        return Result<bool>.Ok(true);
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
        var role = await db.Roles.Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role is null) return Result<RolePermissionsDto>.Fail("الدور غير موجود");
        if (!role.IsSystem) return Result<RolePermissionsDto>.Fail("إعادة التعيين الافتراضي متاحة للأدوار المدمجة فقط");

        // Mirror the seeder's default permission predicates per role name.
        Func<Permission, bool> pick = role.Name switch
        {
            "System Administrator" => _ => true,
            "Manager"         => p => p.Action is PermissionAction.View or PermissionAction.Approve
                                    or PermissionAction.Forward or PermissionAction.Print
                                    || p.Resource == "Reports"
                                    || (p.Resource == "Scanner"
                                        && p.Action is PermissionAction.View or PermissionAction.Edit)
                                    || (p.Resource is "Classification" or "Preservation"
                                        && p.Action == PermissionAction.View),
            "Archive Officer" => p => p.Resource is "Documents" or "Archive"
                                    || (p.Resource is "IncomingMail" or "OutgoingMail"
                                        && p.Action == PermissionAction.View)
                                    || (p.Resource == "Scanner"
                                        && p.Action is PermissionAction.View or PermissionAction.Edit)
                                    || (p.Resource == "Classification"
                                        && p.Action is PermissionAction.View or PermissionAction.Edit)
                                    || (p.Resource == "Preservation"
                                        && p.Action == PermissionAction.View),
            "Employee"        => p => (p.Resource is "Documents" or "IncomingMail" or "OutgoingMail"
                                        && p.Action is PermissionAction.View or PermissionAction.Create
                                            or PermissionAction.Forward)
                                    || (p.Resource == "Scanner"
                                        && p.Action is PermissionAction.View or PermissionAction.Edit),
            _                 => _ => false,
        };

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
        return ToDto(user);
    }

    private static AdminUserListItem ToDto(User u) => new(
        u.Id, u.FullName,
        u.FirstName, u.SecondName, u.ThirdName, u.FamilyName,
        u.Gender.ToString(), u.NationalId,
        u.Email, u.JobTitle, u.Clearance.ToString(), u.IsActive,
        u.UserRoles.Select(ur => ur.Role.Name).OrderBy(n => n).ToList(),
        u.UserRoles.Select(ur => ur.RoleId).ToList());
}
