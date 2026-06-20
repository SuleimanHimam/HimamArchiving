using Archiving.Application.Common.Models;
using Archiving.Application.Features.Users;

namespace Archiving.Application.Common.Interfaces;

public interface IUserAdminService
{
    Task<IReadOnlyList<AdminRoleDto>> ListRolesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminUserListItem>> ListUsersAsync(CancellationToken ct = default);
    Task<Result<AdminUserListItem>> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<Result<AdminUserListItem>> SetRolesAsync(long userId, SetUserRolesRequest request, CancellationToken ct = default);
    Task<Result<AdminUserListItem>> SetActiveAsync(long userId, bool isActive, CancellationToken ct = default);
    Task<Result<bool>> ResetPasswordAsync(long userId, string newPassword, CancellationToken ct = default);

    // Role management
    Task<IReadOnlyList<PermissionInfoDto>> ListPermissionsAsync(CancellationToken ct = default);
    Task<Result<RolePermissionsDto>> GetRolePermissionsAsync(long roleId, CancellationToken ct = default);
    Task<Result<RolePermissionsDto>> SetRolePermissionsAsync(long roleId, SetRolePermissionsRequest req, CancellationToken ct = default);
    Task<Result<RolePermissionsDto>> ResetRolePermissionsAsync(long roleId, CancellationToken ct = default);
    Task<Result<AdminRoleDto>> CreateRoleAsync(CreateRoleRequest req, CancellationToken ct = default);
    Task<Result<bool>> DeleteRoleAsync(long roleId, CancellationToken ct = default);
}
