using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Auth;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
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
        var identifier = request.Email.Trim().ToLowerInvariant();
        var user = await LoadUserGraph().FirstOrDefaultAsync(u => u.Email == identifier, ct);

        // Signing in as "admin" instead of "admin@archiving.local". The part before the @ is what
        // people are told verbally and what they type first, so it is accepted — but only when it
        // names exactly one account. Two users sharing a local part across different domains keep
        // typing the full address rather than one of them silently shadowing the other.
        if (user is null && !identifier.Contains('@') && identifier.Length > 0)
        {
            var prefix = identifier + "@";
            var matches = await LoadUserGraph()
                .Where(u => u.Email.StartsWith(prefix))
                .Take(2)
                .ToListAsync(ct);
            if (matches.Count == 1) user = matches[0];
        }

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

    public async Task<Result<bool>> LogoutAsync(long userId, string? refreshToken, CancellationToken ct = default)
    {
        // Every live refresh token for this user, not only the one the caller happened to send:
        // a session that ends because the screen was left unattended should not leave a usable
        // token behind in another tab that was opened from the same sign-in.
        var live = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);

        // The caller's own token is revoked even if it has somehow already been rotated away from
        // this user's live set, so a logout is never a no-op from the client's point of view.
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var mine = await db.RefreshTokens.FirstOrDefaultAsync(t => t.Token == refreshToken, ct);
            if (mine is not null && mine.RevokedAt is null && !live.Contains(mine)) live.Add(mine);
        }

        foreach (var t in live) t.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<UserDto>> GetCurrentAsync(long userId, CancellationToken ct = default)
    {
        var user = await LoadUserGraph().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null
            ? Result<UserDto>.Fail("المستخدم غير موجود")
            : Result<UserDto>.Ok(ToDto(user));
    }

    public async Task<Result<bool>> ChangePasswordAsync(
        long userId, string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<bool>.Fail("المستخدم غير موجود");

        // Verified before the strength check so a wrong current password never leaks
        // whether the proposed new one would have been accepted.
        if (!hasher.Verify(currentPassword, user.PasswordHash))
            return Result<bool>.Fail("كلمة المرور الحالية غير صحيحة");

        if (!PasswordPolicy.IsStrong(newPassword, out var pwError))
            return Result<bool>.Fail(pwError!);

        if (hasher.Verify(newPassword, user.PasswordHash))
            return Result<bool>.Fail("كلمة المرور الجديدة مطابقة للحالية");

        user.PasswordHash = hasher.Hash(newPassword);

        // Changing a password invalidates every other session: any refresh token still
        // outstanding would otherwise keep a compromised sign-in alive.
        var live = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var t in live) t.RevokedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<UserDto>> SetAvatarAsync(long userId, string? avatarUrl, CancellationToken ct = default)
    {
        var trimmed = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();

        if (trimmed is not null)
        {
            // Only inline images — a remote URL here would let a profile pull content
            // from an arbitrary host on every page render.
            if (!trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                return Result<UserDto>.Fail("صيغة الصورة غير مدعومة");
            if (trimmed.Length > User.AvatarMaxChars)
                return Result<UserDto>.Fail("حجم الصورة كبير جدًا");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<UserDto>.Fail("المستخدم غير موجود");

        user.AvatarUrl = trimmed;
        await db.SaveChangesAsync(ct);

        return await GetCurrentAsync(userId, ct);
    }

    public async Task<Result<UserDto>> SetSealAsync(long userId, SealKind kind, string? imageUrl, CancellationToken ct = default)
    {
        var trimmed = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();

        if (trimmed is not null)
        {
            // Inline images only, for the same reason as the avatar: a remote URL would make
            // every printed letter fetch its signature from an arbitrary host.
            if (!trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                return Result<UserDto>.Fail("صيغة الصورة غير مدعومة");
            if (trimmed.Length > User.SealMaxChars)
                return Result<UserDto>.Fail("حجم الصورة كبير جدًا");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<UserDto>.Fail("المستخدم غير موجود");

        if (kind == SealKind.Signature) user.SignatureUrl = trimmed;
        else user.StampUrl = trimmed;
        await db.SaveChangesAsync(ct);

        return await GetCurrentAsync(userId, ct);
    }

    public async Task<UserSealDto> GetSealAsync(long userId, CancellationToken ct = default)
    {
        var row = await db.Users.AsNoTracking().IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => new UserSealDto(u.Id, u.SignatureUrl, u.StampUrl))
            .FirstOrDefaultAsync(ct);
        // An unknown or unsealed user simply prints without a seal rather than failing the job.
        return row ?? new UserSealDto(userId, null, null);
    }

    // ---- helpers ----

    private IQueryable<User> LoadUserGraph() =>
        db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            // Personal grants ride in the token beside the role-derived ones: the endpoint gate
            // reads claims, so a grant that never reached the token would do nothing.
            .Include(u => u.PermissionGrants).ThenInclude(g => g.Permission)
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

    public async Task<Result<UserDto>> SetDefaultTemplateAsync(
        long userId, long? templateId, ReportTemplateTarget target, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return Result<UserDto>.Fail("المستخدم غير موجود");

        if (templateId is { } id)
        {
            // Only a design this user could already print with may be chosen. Without this the
            // endpoint would let anyone pin another department's private letterhead to their own
            // letters just by knowing its id.
            var t = await db.ReportTemplates
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
            if (t is null) return Result<UserDto>.Fail("التصميم غير موجود");

            var visible = t.Scope switch
            {
                ReportTemplateScope.Default => true,
                ReportTemplateScope.OrgUnit => t.OrgUnitId == user.OrgUnitId,
                ReportTemplateScope.User    => t.UserId == user.Id,
                _ => false,
            };
            if (!visible) return Result<UserDto>.Fail("هذا التصميم غير متاح لك");

            // A design belongs to a register. Pinning a البريد الداخلي design to الصادر الخارجي
            // would store a choice that resolution then silently ignores — better to refuse it.
            if (t.Target != target && t.Target != ReportTemplateTarget.All)
                return Result<UserDto>.Fail("هذا التصميم لسجلٍّ آخر");
        }

        if (target == ReportTemplateTarget.Correspondence) user.DefaultMailTemplateId = templateId;
        else user.DefaultPrintTemplateId = templateId;
        await db.SaveChangesAsync(ct);

        return await GetCurrentAsync(userId, ct);
    }

    private static List<string> RolesOf(User u) =>
        u.UserRoles.Select(ur => ur.Role.Name).ToList();

    /// <summary>What this person may do: everything their roles carry, plus anything granted to
    /// them personally. The two are unioned rather than layered — a grant only ever adds.</summary>
    private static List<string> PermissionsOf(User u) =>
        u.UserRoles.SelectMany(ur => ur.Role.RolePermissions.Select(rp => rp.Permission.Code))
            .Concat(u.PermissionGrants.Select(g => g.Permission.Code))
            .Distinct().ToList();

    private static UserDto ToDto(User u) => new(
        u.Id, u.FullName, u.Email, u.JobTitle, u.Clearance.ToString(),
        RolesOf(u), PermissionsOf(u), u.AvatarUrl, u.SignatureUrl, u.StampUrl, u.OrgUnitId,
        u.DefaultPrintTemplateId, u.DefaultMailTemplateId);
}
