using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/organization")]
[Authorize]
public sealed class OrganizationController(IOrganizationService org) : ControllerBase
{
    // ---- Institutions ----
    [HttpGet("institutions")]
    [HasPermission("Organization.View")]
    public async Task<IActionResult> Institutions(CancellationToken ct) => Ok(await org.ListInstitutionsAsync(ct));

    [HttpPost("institutions")]
    [HasPermission("Organization.Create")]
    public async Task<IActionResult> CreateInstitution([FromBody] CreateInstitutionRequest req, CancellationToken ct)
    {
        var r = await org.CreateInstitutionAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    // ---- Org units ----
    /// <summary>The department list. Readable by any signed-in user: addressing internal mail to a
    /// department, or picking a document's owning unit, needs it, and neither of those is an
    /// administrative act. Creating/editing/deleting units below stays behind Organization.*.
    /// It carries names and codes only — no membership, no contact details.</summary>
    [HttpGet("org-units")]
    public async Task<IActionResult> OrgUnits([FromQuery] long? institutionId, CancellationToken ct)
        => Ok(await org.ListOrgUnitsAsync(institutionId, ct));

    [HttpPost("org-units")]
    [HasPermission("Organization.Create")]
    public async Task<IActionResult> CreateOrgUnit([FromBody] CreateOrgUnitRequest req, CancellationToken ct)
    {
        var r = await org.CreateOrgUnitAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("org-units/{id:long}")]
    [HasPermission("Organization.Edit")]
    public async Task<IActionResult> UpdateOrgUnit(long id, [FromBody] UpdateOrgUnitRequest req, CancellationToken ct)
    {
        var r = await org.UpdateOrgUnitAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("org-units/{id:long}")]
    [HasPermission("Organization.Delete")]
    public async Task<IActionResult> DeleteOrgUnit(long id, CancellationToken ct)
    {
        var r = await org.DeleteOrgUnitAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }

    // ---- Positions ----
    [HttpGet("positions")]
    [HasPermission("Organization.View")]
    public async Task<IActionResult> Positions([FromQuery] long? orgUnitId, CancellationToken ct)
        => Ok(await org.ListPositionsAsync(orgUnitId, ct));

    [HttpPost("positions")]
    [HasPermission("Organization.Create")]
    public async Task<IActionResult> CreatePosition([FromBody] CreatePositionRequest req, CancellationToken ct)
    {
        var r = await org.CreatePositionAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("positions/{id:long}")]
    [HasPermission("Organization.Edit")]
    public async Task<IActionResult> UpdatePosition(long id, [FromBody] UpdatePositionRequest req, CancellationToken ct)
    {
        var r = await org.UpdatePositionAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("positions/{id:long}")]
    [HasPermission("Organization.Delete")]
    public async Task<IActionResult> DeletePosition(long id, CancellationToken ct)
    {
        var r = await org.DeletePositionAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }

    [HttpPost("positions/{id:long}/occupant")]
    [HasPermission("Organization.Edit")]
    public async Task<IActionResult> AssignOccupant(long id, [FromBody] AssignOccupantRequest req, CancellationToken ct)
    {
        var r = await org.AssignOccupantAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    /// <summary>Staff directory used to address correspondence and assign occupants: id, name and
    /// work e-mail of active users, nothing more. Gating it on Organization.View — the permission
    /// for *administering* the org chart — left ordinary staff with an empty recipient picker, so
    /// they could not send internal mail to a colleague at all. Account administration lives in
    /// UsersController, which is permission-gated.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct) => Ok(await org.ListUsersAsync(ct));
}
