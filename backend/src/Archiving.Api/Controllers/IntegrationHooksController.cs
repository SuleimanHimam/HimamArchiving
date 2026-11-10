using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>Outbound integration webhooks: subscribe external systems to domain events (Phase 11).</summary>
[ApiController]
[Route("api/integration-hooks")]
[Authorize]
public sealed class IntegrationHooksController(IIntegrationHookService service) : ControllerBase
{
    [HttpGet]
    [HasPermission("Settings.View")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpPost]
    [HasPermission("Settings.Edit")]
    public async Task<IActionResult> Create([FromBody] CreateIntegrationHookDto dto, CancellationToken ct)
    {
        var r = await service.CreateAsync(dto, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("{id:long}")]
    [HasPermission("Settings.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] CreateIntegrationHookDto dto, CancellationToken ct)
    {
        var r = await service.UpdateAsync(id, dto, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpDelete("{id:long}")]
    [HasPermission("Settings.Edit")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var r = await service.DeleteAsync(id, ct);
        return r.Succeeded ? NoContent() : NotFound(new { error = r.Error });
    }

    [HttpPost("{id:long}/test")]
    [HasPermission("Settings.Edit")]
    public async Task<IActionResult> Test(long id, CancellationToken ct)
    {
        var r = await service.TestAsync(id, ct);
        return r.Succeeded ? Accepted(new { queued = true }) : NotFound(new { error = r.Error });
    }

    [HttpGet("{id:long}/deliveries")]
    [HasPermission("Settings.View")]
    public async Task<IActionResult> Deliveries(long id, CancellationToken ct) => Ok(await service.GetDeliveriesAsync(id, ct));

    [HttpPost("{id:long}/deliveries/{deliveryId:long}/retry")]
    [HasPermission("Settings.Edit")]
    public async Task<IActionResult> Retry(long id, long deliveryId, CancellationToken ct)
    {
        var r = await service.RetryDeliveryAsync(id, deliveryId, ct);
        return r.Succeeded ? Accepted(new { queued = true }) : NotFound(new { error = r.Error });
    }
}
