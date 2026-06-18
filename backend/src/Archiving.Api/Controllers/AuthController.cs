using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService auth, ICurrentUser currentUser) : ControllerBase
{
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

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

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId)
            return Unauthorized();

        var result = await auth.GetCurrentAsync(userId, ct);
        return result.Succeeded ? Ok(result.Value) : NotFound(new { error = result.Error });
    }
}
