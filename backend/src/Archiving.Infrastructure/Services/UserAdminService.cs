using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Users;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
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
        if (string.IsNullOrWhiteSpace(r.Password) || r.Password.Length < 8)
            return Result<AdminUserListItem>.Fail("كلمة المرور يجب ألا تقل عن 8 أحرف");
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            return Result<AdminUserListItem>.Fail("البريد الإلكتروني مستخدم مسبقًا");
        if (r.OrgUnitId is { } ouId && !await db.OrgUnits.AnyAsync(o => o.Id == ouId, ct))
            return Result<AdminUserListItem>.Fail("الوحدة التنظيمية غير موجودة");

        var user = new User
        {
            FullName = r.FullName.Trim(),
            Email = email,
            JobTitle = r.JobTitle?.Trim() ?? string.Empty,
            OrgUnitId = r.OrgUnitId,
            PasswordHash = hasher.Hash(r.Password),
            Clearance = r.Clearance,
            IsActive = true,
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
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            return Result<bool>.Fail("كلمة المرور يجب ألا تقل عن 8 أحرف");
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<bool>.Fail("المستخدم غير موجود");

        user.PasswordHash = hasher.Hash(newPassword);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PasswordReset", "User", userId, user.FullName, ct: ct);
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
        u.Id, u.FullName, u.Email, u.JobTitle, u.Clearance.ToString(), u.IsActive,
        u.UserRoles.Select(ur => ur.Role.Name).OrderBy(n => n).ToList(),
        u.UserRoles.Select(ur => ur.RoleId).ToList());
}
