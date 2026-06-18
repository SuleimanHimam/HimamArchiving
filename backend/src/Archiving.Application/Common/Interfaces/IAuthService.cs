using Archiving.Application.Common.Models;
using Archiving.Application.Features.Auth;

namespace Archiving.Application.Common.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default);
    Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct = default);
    Task<Result<UserDto>> GetCurrentAsync(long userId, CancellationToken ct = default);
}
