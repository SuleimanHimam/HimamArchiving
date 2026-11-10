using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.External;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>External organizations &amp; contacts directory (EXT, v1.1) — correspondence parties.
/// Gated on the existing Organization permissions.</summary>
[ApiController]
[Route("api/external")]
[Authorize]
public sealed class ExternalDirectoryController(IExternalDirectoryService service) : ControllerBase
{
    // ── Organizations ──
    [HttpGet("organizations")]
    [HasPermission("Organization.View")]
    public async Task<IActionResult> ListOrgs([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await service.ListOrgsAsync(search, page, pageSize, ct));

    [HttpGet("organizations/{id:long}")]
    [HasPermission("Organization.View")]
    public async Task<IActionResult> GetOrg(long id, CancellationToken ct)
    {
        var r = await service.GetOrgAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpPost("organizations")]
    [HasPermission("Organization.Create")]
    public async Task<IActionResult> CreateOrg([FromBody] CreateExternalOrgDto dto, CancellationToken ct)
    {
        var r = await service.CreateOrgAsync(dto, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("organizations/{id:long}")]
    [HasPermission("Organization.Edit")]
    public async Task<IActionResult> UpdateOrg(long id, [FromBody] CreateExternalOrgDto dto, CancellationToken ct)
    {
        var r = await service.UpdateOrgAsync(id, dto, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpDelete("organizations/{id:long}")]
    [HasPermission("Organization.Delete")]
    public async Task<IActionResult> DeleteOrg(long id, CancellationToken ct)
    {
        var r = await service.DeleteOrgAsync(id, ct);
        return r.Succeeded ? NoContent() : NotFound(new { error = r.Error });
    }

    // ── Contacts ──
    [HttpGet("contacts")]
    [HasPermission("Organization.View")]
    public async Task<IActionResult> ListContacts([FromQuery] long? organizationId, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await service.ListContactsAsync(organizationId, search, page, pageSize, ct));

    [HttpPost("contacts")]
    [HasPermission("Organization.Create")]
    public async Task<IActionResult> CreateContact([FromBody] CreateExternalContactDto dto, CancellationToken ct)
    {
        var r = await service.CreateContactAsync(dto, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("contacts/{id:long}")]
    [HasPermission("Organization.Edit")]
    public async Task<IActionResult> UpdateContact(long id, [FromBody] CreateExternalContactDto dto, CancellationToken ct)
    {
        var r = await service.UpdateContactAsync(id, dto, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpDelete("contacts/{id:long}")]
    [HasPermission("Organization.Delete")]
    public async Task<IActionResult> DeleteContact(long id, CancellationToken ct)
    {
        var r = await service.DeleteContactAsync(id, ct);
        return r.Succeeded ? NoContent() : NotFound(new { error = r.Error });
    }
}
