using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Auth;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Archiving.Infrastructure.Auth;

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher hasher,
    IJwtTokenService tokens,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, string? ip, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await LoadUserGraph().FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
            return Result<AuthResponse>.Fail("بيانات الدخول غير صحيحة"); // invalid credentials

        if (!user.IsActive)
            return Result<AuthResponse>.Fail("الحساب غير مفعّل"); // account disabled

        user.LastLoginAt = DateTime.UtcNow;
        var response = await IssueAsync(user, ip, ct);
        return Result<AuthResponse>.Ok(response);
    }

    public async Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, string? ip, CancellationToken ct = default)
    {
        var token = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken, ct);

        if (token is null || !token.IsActive)
            return Result<AuthResponse>.Fail("رمز التحديث غير صالح"); // invalid refresh token

        token.RevokedAt = DateTime.UtcNow; // rotate

        var user = await LoadUserGraph().FirstAsync(u => u.Id == token.UserId, ct);
        var response = await IssueAsync(user, ip, ct);
        return Result<AuthResponse>.Ok(response);
    }

    public async Task<Result<UserDto>> GetCurrentAsync(long userId, CancellationToken ct = default)
    {
        var user = await LoadUserGraph().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null
            ? Result<UserDto>.Fail("المستخدم غير موجود")
            : Result<UserDto>.Ok(ToDto(user));
    }

    // ---- helpers ----

    private IQueryable<User> LoadUserGraph() =>
        db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .AsSplitQuery();

    private async Task<AuthResponse> IssueAsync(User user, string? ip, CancellationToken ct)
    {
        var roles = RolesOf(user);
        var permissions = PermissionsOf(user);

        var (accessToken, expiresAt) = tokens.CreateAccessToken(user, roles, permissions);
        var refresh = tokens.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refresh,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays),
            CreatedByIp = ip,
        });

        await db.SaveChangesAsync(ct);
        return new AuthResponse(accessToken, refresh, expiresAt, ToDto(user));
    }

    private static List<string> RolesOf(User u) =>
        u.UserRoles.Select(ur => ur.Role.Name).ToList();

    private static List<string> PermissionsOf(User u) =>
        u.UserRoles.SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code))
            .Distinct().ToList();

    private static UserDto ToDto(User u) => new(
        u.Id, u.FullName, u.Email, u.JobTitle, u.Clearance.ToString(),
        RolesOf(u), PermissionsOf(u));
}
