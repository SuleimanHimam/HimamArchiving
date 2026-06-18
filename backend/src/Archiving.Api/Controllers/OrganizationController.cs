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
    [HttpGet("org-units")]
    [HasPermission("Organization.View")]
    public async Task<IActionResult> OrgUnits([FromQuery] long? institutionId, CancellationToken ct)
        => Ok(await org.ListOrgUnitsAsync(institutionId, ct));

    [HttpPost("org-units")]
    [HasPermission("Organization.Create")]
    public async Task<IActionResult> CreateOrgUnit([FromBody] CreateOrgUnitRequest req, CancellationToken ct)
    {
        var r = await org.CreateOrgUnitAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
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

    [HttpPost("positions/{id:long}/occupant")]
    [HasPermission("Organization.Edit")]
    public async Task<IActionResult> AssignOccupant(long id, [FromBody] AssignOccupantRequest req, CancellationToken ct)
    {
        var r = await org.AssignOccupantAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    // ---- Users (lookup for occupant assignment) ----
    [HttpGet("users")]
    [HasPermission("Organization.View")]
    public async Task<IActionResult> Users(CancellationToken ct) => Ok(await org.ListUsersAsync(ct));
}
