using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

public sealed record LicenseStatusDto(
    string    State,
    bool      IsUsable,
    string    MachineId,
    string?   LicenseId,
    string?   Customer,
    string?   Edition,
    DateTime? IssuedAt,
    DateTime? ExpiresAt,
    int?      DaysRemaining,
    int?      MaxUsers,
    string[]  Features,
    string    Message);

public sealed record InstallLicenseRequest(string License);

/// <summary>Licence status and installation. Reachable while the instance is unlicensed — the
/// enforcement middleware exempts this route on purpose, so an expired server can be renewed from
/// its own admin screen.</summary>
[ApiController]
[Route("api/license")]
[Authorize]
public sealed class LicenseController(ILicenseService license, IAuditWriter audit) : ControllerBase
{
    private static LicenseStatusDto ToDto(LicenseStatus s) => new(
        s.State.ToString(), s.IsUsable, s.MachineId, s.LicenseId, s.Customer, s.Edition,
        s.IssuedAt, s.ExpiresAt, s.DaysRemaining, s.MaxUsers, [.. s.Features], s.Message);

    /// <summary>Current licence state. Any signed-in user may read it: the SPA shows an expiry banner
    /// to everyone, and it discloses nothing sensitive.</summary>
    [HttpGet("status")]
    public ActionResult<LicenseStatusDto> Status() => ToDto(license.GetStatus());

    /// <summary>The fingerprint of this server, which is what the customer sends us to obtain a licence.</summary>
    [HttpGet("machine-id")]
    [HasPermission("Settings.View")]
    public ActionResult<object> MachineId() => Ok(new { machineId = license.MachineId });

    /// <summary>Installs a licence key. Rejected — and nothing written — unless it is correctly signed
    /// and bound to this machine.</summary>
    [HttpPost]
    [HasPermission("Settings.Edit")]
    public async Task<ActionResult<LicenseStatusDto>> Install([FromBody] InstallLicenseRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.License))
            return BadRequest(new { message = "Licence key is required." });

        var status = license.Install(request.License);
        if (!status.IsUsable)
            return BadRequest(ToDto(status));

        await audit.WriteAsync("LicenseInstalled", "License", 0,
            $"{status.Customer} / {status.Edition} ({status.LicenseId}), " +
            $"expires {status.ExpiresAt?.ToString("yyyy-MM-dd") ?? "never"}");
        return Ok(ToDto(status));
    }
}
