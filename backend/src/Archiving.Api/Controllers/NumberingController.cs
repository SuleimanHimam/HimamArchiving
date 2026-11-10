using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Numbering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>Admin configuration of auto-numbering templates (Phase 9).</summary>
[ApiController]
[Route("api/numbering-templates")]
[Authorize]
public sealed class NumberingController(INumberingService service) : ControllerBase
{
    [HttpGet]
    [HasPermission("Settings.View")]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.ListAsync(ct));

    [HttpPost]
    [HasPermission("Settings.Edit")]
    public async Task<IActionResult> Create([FromBody] CreateNumberingTemplateDto dto, CancellationToken ct)
    {
        var r = await service.CreateAsync(dto, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("{id:long}")]
    [HasPermission("Settings.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] CreateNumberingTemplateDto dto, CancellationToken ct)
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

    [HttpPost("{id:long}/reset")]
    [HasPermission("Settings.Edit")]
    public async Task<IActionResult> Reset(long id, CancellationToken ct)
    {
        var r = await service.ResetAsync(id, ct);
        return r.Succeeded ? NoContent() : NotFound(new { error = r.Error });
    }

    [HttpGet("preview")]
    [HasPermission("Settings.View")]
    public IActionResult Preview([FromQuery] string pattern)
        => Ok(new { pattern, sample = service.Preview(pattern ?? "") });
}
