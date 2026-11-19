using Archiving.Api.Authorization;
using Archiving.Application.Features.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers.Core;

// Every action declares its permission explicitly; CorePermissionTests fails the build otherwise.

[ApiController]
[Route("api/core/sites")]
[Authorize]
public sealed class SitesController(ISiteService sites) : ControllerBase
{
    [HttpGet]
    [HasPermission("Sites.View")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await sites.ListAsync(ct));

    [HttpPost]
    [HasPermission("Sites.Create")]
    public async Task<IActionResult> Create([FromBody] SaveSiteRequest request, CancellationToken ct)
    {
        var r = await sites.CreateAsync(request, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("{id:long}")]
    [HasPermission("Sites.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveSiteRequest request, CancellationToken ct)
    {
        var r = await sites.UpdateAsync(id, request, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("{id:long}")]
    [HasPermission("Sites.Delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var r = await sites.DeleteAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }

    [HttpGet("{siteId:long}/locations")]
    [HasPermission("Sites.View")]
    public async Task<IActionResult> Locations(long siteId, CancellationToken ct) =>
        Ok(await sites.ListLocationsAsync(siteId, ct));

    [HttpPost("{siteId:long}/locations")]
    [HasPermission("Sites.Create")]
    public async Task<IActionResult> CreateLocation(long siteId, [FromBody] SaveSiteLocationRequest request, CancellationToken ct)
    {
        var r = await sites.CreateLocationAsync(siteId, request, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("locations/{id:long}")]
    [HasPermission("Sites.Edit")]
    public async Task<IActionResult> UpdateLocation(long id, [FromBody] SaveSiteLocationRequest request, CancellationToken ct)
    {
        var r = await sites.UpdateLocationAsync(id, request, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("locations/{id:long}")]
    [HasPermission("Sites.Delete")]
    public async Task<IActionResult> DeleteLocation(long id, CancellationToken ct)
    {
        var r = await sites.DeleteLocationAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }
}

[ApiController]
[Route("api/core/employees")]
[Authorize]
public sealed class EmployeesController(IEmployeeService employees) : ControllerBase
{
    [HttpGet]
    [HasPermission("Employees.View")]
    public async Task<IActionResult> List([FromQuery] EmployeeListQuery query, CancellationToken ct) =>
        Ok(await employees.ListAsync(query, ct));

    [HttpGet("{id:long}")]
    [HasPermission("Employees.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var r = await employees.GetAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpPost]
    [HasPermission("Employees.Create")]
    public async Task<IActionResult> Create([FromBody] SaveEmployeeRequest request, CancellationToken ct)
    {
        var r = await employees.CreateAsync(request, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("{id:long}")]
    [HasPermission("Employees.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveEmployeeRequest request, CancellationToken ct)
    {
        var r = await employees.UpdateAsync(id, request, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("{id:long}")]
    [HasPermission("Employees.Delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var r = await employees.DeleteAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }
}
