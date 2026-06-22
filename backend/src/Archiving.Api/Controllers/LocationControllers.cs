using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Locations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/buildings")]
public sealed class BuildingsController(ILocationService svc) : ControllerBase
{
    [HttpGet, HasPermission("Archive.View")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await svc.ListBuildingsAsync(ct));

    [HttpPost, HasPermission("Archive.Create")]
    public async Task<IActionResult> Create([FromBody] BuildingRequest r, CancellationToken ct)
    { var x = await svc.CreateBuildingAsync(r, ct); return x.Succeeded ? Ok(x.Value) : BadRequest(new { error = x.Error }); }

    [HttpPut("{id:long}"), HasPermission("Archive.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] BuildingRequest r, CancellationToken ct)
    { var x = await svc.UpdateBuildingAsync(id, r, ct); return x.Succeeded ? Ok(x.Value) : BadRequest(new { error = x.Error }); }

    [HttpDelete("{id:long}"), HasPermission("Archive.Delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    { var x = await svc.DeleteBuildingAsync(id, ct); return x.Succeeded ? NoContent() : BadRequest(new { error = x.Error }); }
}

[ApiController]
[Authorize]
[Route("api/rooms")]
public sealed class RoomsController(ILocationService svc) : ControllerBase
{
    [HttpGet, HasPermission("Archive.View")]
    public async Task<IActionResult> List([FromQuery] long? buildingId, CancellationToken ct) => Ok(await svc.ListRoomsAsync(buildingId, ct));

    [HttpPost, HasPermission("Archive.Create")]
    public async Task<IActionResult> Create([FromBody] RoomRequest r, CancellationToken ct)
    { var x = await svc.CreateRoomAsync(r, ct); return x.Succeeded ? Ok(x.Value) : BadRequest(new { error = x.Error }); }

    [HttpPut("{id:long}"), HasPermission("Archive.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] RoomRequest r, CancellationToken ct)
    { var x = await svc.UpdateRoomAsync(id, r, ct); return x.Succeeded ? Ok(x.Value) : BadRequest(new { error = x.Error }); }

    [HttpDelete("{id:long}"), HasPermission("Archive.Delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    { var x = await svc.DeleteRoomAsync(id, ct); return x.Succeeded ? NoContent() : BadRequest(new { error = x.Error }); }

    [HttpGet("{id:long}/connections"), HasPermission("Archive.View")]
    public async Task<IActionResult> Connections(long id, CancellationToken ct) => Ok(await svc.ListConnectionsAsync(id, ct));

    [HttpPost("{id:long}/connections"), HasPermission("Archive.Edit")]
    public async Task<IActionResult> AddConnection(long id, [FromBody] RoomConnectionRequest r, CancellationToken ct)
    { var x = await svc.AddConnectionAsync(id, r, ct); return x.Succeeded ? Ok(x.Value) : BadRequest(new { error = x.Error }); }

    [HttpDelete("{id:long}/connections/{connectionId:long}"), HasPermission("Archive.Edit")]
    public async Task<IActionResult> RemoveConnection(long id, long connectionId, CancellationToken ct)
    { var x = await svc.RemoveConnectionAsync(id, connectionId, ct); return x.Succeeded ? NoContent() : BadRequest(new { error = x.Error }); }
}

[ApiController]
[Authorize]
[Route("api/cabinets")]
public sealed class CabinetsController(ILocationService svc) : ControllerBase
{
    [HttpGet, HasPermission("Archive.View")]
    public async Task<IActionResult> List([FromQuery] long? roomId, CancellationToken ct) => Ok(await svc.ListCabinetsAsync(roomId, ct));

    [HttpPost, HasPermission("Archive.Create")]
    public async Task<IActionResult> Create([FromBody] CabinetRequest r, CancellationToken ct)
    { var x = await svc.CreateCabinetAsync(r, ct); return x.Succeeded ? Ok(x.Value) : BadRequest(new { error = x.Error }); }

    [HttpPut("{id:long}"), HasPermission("Archive.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] CabinetRequest r, CancellationToken ct)
    { var x = await svc.UpdateCabinetAsync(id, r, ct); return x.Succeeded ? Ok(x.Value) : BadRequest(new { error = x.Error }); }

    [HttpDelete("{id:long}"), HasPermission("Archive.Delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    { var x = await svc.DeleteCabinetAsync(id, ct); return x.Succeeded ? NoContent() : BadRequest(new { error = x.Error }); }
}

[ApiController]
[Authorize]
[Route("api/shelves")]
public sealed class ShelvesController(ILocationService svc) : ControllerBase
{
    [HttpGet, HasPermission("Archive.View")]
    public async Task<IActionResult> List([FromQuery] long? cabinetId, CancellationToken ct) => Ok(await svc.ListShelvesAsync(cabinetId, ct));

    [HttpPost, HasPermission("Archive.Create")]
    public async Task<IActionResult> Create([FromBody] ShelfRequest r, CancellationToken ct)
    { var x = await svc.CreateShelfAsync(r, ct); return x.Succeeded ? Ok(x.Value) : BadRequest(new { error = x.Error }); }

    [HttpPut("{id:long}"), HasPermission("Archive.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] ShelfRequest r, CancellationToken ct)
    { var x = await svc.UpdateShelfAsync(id, r, ct); return x.Succeeded ? Ok(x.Value) : BadRequest(new { error = x.Error }); }

    [HttpDelete("{id:long}"), HasPermission("Archive.Delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    { var x = await svc.DeleteShelfAsync(id, ct); return x.Succeeded ? NoContent() : BadRequest(new { error = x.Error }); }
}

[ApiController]
[Authorize]
[Route("api/boxes")]
public sealed class BoxesController(ILocationService svc) : ControllerBase
{
    [HttpGet, HasPermission("Archive.View")]
    public async Task<IActionResult> List([FromQuery] long? shelfId, [FromQuery] long? roomId, CancellationToken ct) => Ok(await svc.ListBoxesAsync(shelfId, roomId, ct));

    [HttpPost, HasPermission("Archive.Create")]
    public async Task<IActionResult> Create([FromBody] BoxRequest r, CancellationToken ct)
    { var x = await svc.CreateBoxAsync(r, ct); return x.Succeeded ? Ok(x.Value) : BadRequest(new { error = x.Error }); }

    [HttpPut("{id:long}"), HasPermission("Archive.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] BoxRequest r, CancellationToken ct)
    { var x = await svc.UpdateBoxAsync(id, r, ct); return x.Succeeded ? Ok(x.Value) : BadRequest(new { error = x.Error }); }

    [HttpDelete("{id:long}"), HasPermission("Archive.Delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    { var x = await svc.DeleteBoxAsync(id, ct); return x.Succeeded ? NoContent() : BadRequest(new { error = x.Error }); }
}

[ApiController]
[Authorize]
[Route("api/locations")]
public sealed class LocationsController(ILocationService svc) : ControllerBase
{
    [HttpGet("tree"), HasPermission("Archive.View")]
    public async Task<IActionResult> Tree(CancellationToken ct) => Ok(await svc.GetTreeAsync(ct));

    [HttpGet("{boxId:long}/breadcrumb"), HasPermission("Archive.View")]
    public async Task<IActionResult> Breadcrumb(long boxId, CancellationToken ct)
    { var x = await svc.GetBreadcrumbAsync(boxId, ct); return x.Succeeded ? Ok(x.Value) : NotFound(new { error = x.Error }); }

    [HttpGet("{boxId:long}/ancestry"), HasPermission("Archive.View")]
    public async Task<IActionResult> Ancestry(long boxId, CancellationToken ct)
    { var x = await svc.GetBoxAncestryAsync(boxId, ct); return x.Succeeded ? Ok(x.Value) : NotFound(new { error = x.Error }); }

    [HttpPost("migrate-legacy"), HasPermission("Archive.Edit")]
    public async Task<IActionResult> MigrateLegacy(CancellationToken ct)
    { var x = await svc.MigrateLegacyAsync(ct); return x.Succeeded ? Ok(new { message = x.Value }) : BadRequest(new { error = x.Error }); }
}
