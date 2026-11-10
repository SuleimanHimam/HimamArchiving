using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Escalation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>Admin configuration of escalation rules + escalation history (Phase 6).</summary>
[ApiController]
[Route("api")]
[Authorize]
public sealed class EscalationController(IEscalationService service) : ControllerBase
{
    [HttpGet("escalation-rules")]
    [HasPermission("Workflow.View")]
    public async Task<IActionResult> ListRules(CancellationToken ct) => Ok(await service.ListRulesAsync(ct));

    [HttpPost("escalation-rules")]
    [HasPermission("Workflow.Edit")]
    public async Task<IActionResult> Create([FromBody] CreateEscalationRuleDto dto, CancellationToken ct)
    {
        var r = await service.CreateRuleAsync(dto, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("escalation-rules/{id:long}")]
    [HasPermission("Workflow.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] CreateEscalationRuleDto dto, CancellationToken ct)
    {
        var r = await service.UpdateRuleAsync(id, dto, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpDelete("escalation-rules/{id:long}")]
    [HasPermission("Workflow.Edit")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var r = await service.DeleteRuleAsync(id, ct);
        return r.Succeeded ? NoContent() : NotFound(new { error = r.Error });
    }

    [HttpGet("escalation-log")]
    [HasPermission("Workflow.View")]
    public async Task<IActionResult> Log(
        [FromQuery] long? workflowDefinitionId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
        => Ok(await service.GetLogAsync(workflowDefinitionId, from, to, ct));
}
