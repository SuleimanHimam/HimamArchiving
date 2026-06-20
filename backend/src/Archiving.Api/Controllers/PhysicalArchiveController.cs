using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Physical;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/physical-archive")]
[Authorize]
public sealed class PhysicalArchiveController(IPhysicalArchiveService service) : ControllerBase
{
    [HttpGet("locations")]
    [HasPermission("Archive.View")]
    public async Task<IActionResult> Locations(CancellationToken ct) => Ok(await service.ListLocationsAsync(ct));

    [HttpPost("locations")]
    [HasPermission("Archive.Create")]
    public async Task<IActionResult> CreateLocation([FromBody] CreatePhysicalLocationRequest req, CancellationToken ct)
    {
        var r = await service.CreateLocationAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("locations/{id:long}")]
    [HasPermission("Archive.Create")]
    public async Task<IActionResult> UpdateLocation(long id, [FromBody] UpdatePhysicalLocationRequest req, CancellationToken ct)
    {
        var r = await service.UpdateLocationAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("locations/{id:long}")]
    [HasPermission("Archive.Create")]
    public async Task<IActionResult> DeleteLocation(long id, CancellationToken ct)
    {
        var r = await service.DeleteLocationAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }

    [HttpGet("items")]
    [HasPermission("Archive.View")]
    public async Task<IActionResult> Items([FromQuery] long? locationId, CancellationToken ct)
        => Ok(await service.ListItemsAsync(locationId, ct));

    [HttpPost("items")]
    [HasPermission("Archive.Archive")]
    public async Task<IActionResult> CreateItem([FromBody] CreatePhysicalArchiveItemRequest req, CancellationToken ct)
    {
        var r = await service.CreateItemAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("items/{id:long}")]
    [HasPermission("Archive.Archive")]
    public async Task<IActionResult> UpdateItem(long id, [FromBody] UpdatePhysicalArchiveItemRequest req, CancellationToken ct)
    {
        var r = await service.UpdateItemAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("items/{id:long}")]
    [HasPermission("Archive.Archive")]
    public async Task<IActionResult> DeleteItem(long id, CancellationToken ct)
    {
        var r = await service.DeleteItemAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }
}
