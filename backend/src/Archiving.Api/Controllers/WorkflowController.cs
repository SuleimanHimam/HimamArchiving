using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/workflow")]
[Authorize]
public sealed class WorkflowController(IWorkflowService service) : ControllerBase
{
    // ---- Definitions (admin) ----
    [HttpGet("definitions")]
    [HasPermission("Workflow.View")]
    public async Task<IActionResult> Definitions(CancellationToken ct) => Ok(await service.ListDefinitionsAsync(ct));

    [HttpGet("definitions/{id:long}")]
    [HasPermission("Workflow.View")]
    public async Task<IActionResult> Definition(long id, CancellationToken ct)
    {
        var r = await service.GetDefinitionAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    [HttpPost("definitions")]
    [HasPermission("Workflow.Create")]
    public async Task<IActionResult> CreateDefinition([FromBody] CreateWorkflowDefinitionRequest req, CancellationToken ct)
    {
        var r = await service.CreateDefinitionAsync(req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(Definition), new { id = r.Value!.Id }, r.Value)
            : BadRequest(new { error = r.Error });
    }

    // ---- Instances ----
    [HttpPost("instances")]
    [HasPermission("Workflow.Create")]
    public async Task<IActionResult> Start([FromBody] StartWorkflowRequest req, CancellationToken ct)
    {
        var r = await service.StartAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpGet("instances/{id:long}")]
    [HasPermission("Workflow.View")]
    public async Task<IActionResult> Instance(long id, CancellationToken ct)
    {
        var r = await service.GetInstanceAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    // ---- Worklist (the caller's own open tasks) ----
    [HttpGet("my-tasks")]
    public async Task<IActionResult> MyTasks(CancellationToken ct) => Ok(await service.MyWorklistAsync(ct));

    [HttpPost("tasks/{taskId:long}/actions")]
    public async Task<IActionResult> Act(long taskId, [FromBody] WorkflowActionRequest req, CancellationToken ct)
    {
        var r = await service.ActOnTaskAsync(taskId, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }
}
