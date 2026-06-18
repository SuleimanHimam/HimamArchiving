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
}
