using Archiving.Application.Common.Models;
using Archiving.Domain.Enums;
using Archiving.Application.Features.Auth;

namespace Archiving.Application.Common.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default);
    Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct = default);
    Task<Result<UserDto>> GetCurrentAsync(long userId, CancellationToken ct = default);

    /// <summary>
    /// Ends a session server-side: revokes every refresh token the user still holds, so the
    /// session cannot be resumed after the client has forgotten it. The access token itself is a
    /// signed JWT and stays valid until it expires — revoking those would need a deny-list on
    /// every request, which this system does not carry. Access-token lifetime is therefore what
    /// bounds the window, and it is set deliberately short relative to the client's idle timeout
    /// (see Jwt:AccessTokenMinutes).
    /// </summary>
    Task<Result<bool>> LogoutAsync(long userId, string? refreshToken, CancellationToken ct = default);

    /// <summary>Changes the caller's own password after verifying the current one.</summary>
    Task<Result<bool>> ChangePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken ct = default);

    /// <summary>Sets or clears the caller's own profile picture. Returns the refreshed user.</summary>
    Task<Result<UserDto>> SetAvatarAsync(long userId, string? avatarUrl, CancellationToken ct = default);

    /// <summary>Sets or clears one of a user's seal images (توقيع / ختم). Self-service only —
    /// the caller may set their own.</summary>
    Task<Result<UserDto>> SetSealAsync(long userId, SealKind kind, string? imageUrl, CancellationToken ct = default);

    /// <summary>The seal images to stamp on a letter this user sent. Returns empty images rather
    /// than failing when the user is unknown or has set none.</summary>
    Task<UserSealDto> GetSealAsync(long userId, CancellationToken ct = default);

    /// <summary>Chooses (or, with null, clears) the caller's own default print design.</summary>
    Task<Result<UserDto>> SetDefaultTemplateAsync(
        long userId, long? templateId, ReportTemplateTarget target, CancellationToken ct = default);
}
