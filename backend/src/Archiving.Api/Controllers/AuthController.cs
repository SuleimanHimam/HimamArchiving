using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Auth;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

/// <summary>A try-it account offered on the login screen. Carries its password: see
/// <see cref="AuthController.DemoAccounts"/> for why that is safe only in development.</summary>
public sealed record DemoAccountDto(string Email, string Password, string FullName, string JobTitle, string Role);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService auth, ICurrentUser currentUser, AppDbContext db, IWebHostEnvironment env) : ControllerBase
{
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    /// <summary>Accounts the login page offers as one-click fills, so the system can be tried from
    /// each role's point of view without anyone handing out credentials by hand.
    ///
    /// Development only, and deliberately so: the response contains passwords in clear. Outside
    /// development it is an empty list — not a 404 — so the login page simply renders no buttons
    /// rather than reporting an error for a feature that is meant to be absent.
    ///
    /// Only accounts that exist and are active are listed, so a demo user someone deactivated
    /// stops being offered instead of failing at the login itself.</summary>
    [HttpGet("demo-accounts")]
    [AllowAnonymous]
    public async Task<IActionResult> DemoAccounts(CancellationToken ct)
    {
        if (!env.IsDevelopment()) return Ok(Array.Empty<DemoAccountDto>());

        // One query per account rather than an IN over the emails: this provider cannot translate a
        // parameterised collection, and the list is four rows long.
        var offered = new List<DemoAccountDto>();
        foreach (var d in DbSeeder.DemoAccounts)
            if (await db.Users.AnyAsync(u => u.Email == d.Email && u.IsActive, ct))
                offered.Add(new DemoAccountDto(d.Email, d.Password, d.FullName, d.JobTitle, d.RoleName));

        return Ok(offered);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, Ip, ct);
        return result.Succeeded ? Ok(result.Value) : Unauthorized(new { error = result.Error });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await auth.RefreshAsync(request, Ip, ct);
        return result.Succeeded ? Ok(result.Value) : Unauthorized(new { error = result.Error });
    }

    /// <summary>
    /// Ends the session server-side. Called when the user signs out and when the client's idle
    /// timer expires — best-effort in both cases: the client clears its own storage first and does
    /// not wait for this, because a session must end even with the network down.
    ///
    /// Revokes the caller's refresh tokens. The access token remains valid until it expires, since
    /// verifying a revocation list on every request is not something this system does; that window
    /// is bounded by Jwt:AccessTokenMinutes, which is set deliberately against the client's idle
    /// timeout rather than left at a library default.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? req, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Unauthorized();

        await auth.LogoutAsync(userId, req?.RefreshToken, ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return Unauthorized();

        var result = await auth.GetCurrentAsync(userId, ct);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    /// <summary>Self-service password change. No permission gate — it only ever
    /// affects the caller's own account, and requires the current password.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return Unauthorized();

        var r = await auth.ChangePasswordAsync(userId, req.CurrentPassword, req.NewPassword, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }

    /// <summary>Sets or clears the caller's own profile picture.</summary>
    [HttpPut("avatar")]
    [Authorize]
    public async Task<IActionResult> SetAvatar([FromBody] SetAvatarRequest req, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return Unauthorized();

        var r = await auth.SetAvatarAsync(userId, req.AvatarUrl, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Sets or clears the caller's own signature (توقيع) or stamp (ختم).
    /// Self-service only: there is deliberately no path to set someone else's seal.</summary>
    [HttpPut("seal")]
    [Authorize]
    public async Task<IActionResult> SetSeal([FromBody] SetSealRequest req, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return Unauthorized();

        var r = await auth.SetSealAsync(userId, req.Kind, req.ImageUrl, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Chooses the caller's own default print design — the letterhead their outgoing
    /// letters are produced on. Self-service: the body carries no user id.</summary>
    [HttpPut("default-template")]
    [Authorize]
    public async Task<IActionResult> SetDefaultTemplate([FromBody] SetDefaultTemplateRequest req, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return Unauthorized();

        var r = await auth.SetDefaultTemplateAsync(
            userId, req.TemplateId, (ReportTemplateTarget)req.Target, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>The seal images used to sign a letter that <paramref name="userId"/> sent.
    /// Any signed-in user may read these: printing someone's letter has to reproduce their
    /// signature, and the images carry no more weight than the letterhead they sit on.</summary>
    [HttpGet("users/{userId:long}/seal")]
    [Authorize]
    public async Task<IActionResult> GetSeal(long userId, CancellationToken ct)
        => Ok(await auth.GetSealAsync(userId, ct));
}
