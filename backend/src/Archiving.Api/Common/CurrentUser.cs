using System.Security.Claims;
using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Enums;

namespace Archiving.Api.Common;

/// <summary>Resolves the authenticated caller from the JWT claims on the current request.</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public long? UserId =>
        long.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public string? Email => Principal?.FindFirstValue(ClaimTypes.Email);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public ConfidentialityLevel Clearance =>
        Enum.TryParse<ConfidentialityLevel>(Principal?.FindFirstValue("clearance"), out var c)
            ? c : ConfidentialityLevel.Public;

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
