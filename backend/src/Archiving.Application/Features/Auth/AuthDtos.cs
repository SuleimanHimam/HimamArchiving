namespace Archiving.Application.Features.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

/// <summary>Body of the logout call. The refresh token is optional — the caller is identified by
/// their access token, and a client that has already dropped its refresh token can still sign out.</summary>
public sealed record LogoutRequest(string? RefreshToken = null);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User);

public sealed record UserDto(
    long Id,
    string FullName,
    string Email,
    string JobTitle,
    string Clearance,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    string? AvatarUrl = null,
    /// <summary>The caller's own signature and stamp, so the account screen can show what is
    /// set. Only ever populated for the signed-in user — never for a list of other people.</summary>
    string? SignatureUrl = null,
    string? StampUrl = null,
    /// <summary>The caller's department. Print designs resolve User → OrgUnit → Default, so the
    /// client needs this to preview an unsaved letter on the same letterhead it will print on.</summary>
    long? OrgUnitId = null,
    /// <summary>The design the caller chose for their outgoing letters, or null to use the
    /// positional fallback. Only ever populated for the signed-in user.</summary>
    long? DefaultPrintTemplateId = null,
    /// <summary>The same, for internal messages.</summary>
    long? DefaultMailTemplateId = null);

/// <summary>The design to use for the caller's outgoing letters, or null to clear the choice.</summary>
/// <param name="Target">Which register the choice is for — mirrors ReportTemplateTarget:
/// 0 = Correspondence (البريد الداخلي), 1 = Document (الصادر الخارجي).</param>
public sealed record SetDefaultTemplateRequest(long? TemplateId, int Target = 1);

/// <summary>Self-service password change — proves possession of the current one.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Data URI for the new picture, or null to remove it.</summary>
public sealed record SetAvatarRequest(string? AvatarUrl);

/// <summary>Which of the caller's seal images is being set.</summary>
public enum SealKind { Signature = 0, Stamp = 1 }

/// <summary>Data URI for the new signature/stamp, or null to remove it.</summary>
public sealed record SetSealRequest(SealKind Kind, string? ImageUrl);

/// <summary>An employee's seal images, read at print time to sign the letter they sent.</summary>
public sealed record UserSealDto(long UserId, string? SignatureUrl, string? StampUrl);
