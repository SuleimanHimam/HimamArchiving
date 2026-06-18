namespace Archiving.Application.Features.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

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
    IReadOnlyCollection<string> Permissions);
