using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

/// <summary>
/// The workflow engine's API: admin-defined routes, live runs, and the caller's own worklist.
///
/// <para>Nothing here delivers messages. A run addresses its participants through المراسلات — see
/// <c>IWorkflowMailRouter</c> — so these endpoints exist to configure routes, to act on a task, and to
/// let a correspondence view ask what workflow it belongs to.</para>
/// </summary>
[ApiController]
[Route("api/workflow")]
[Authorize]
public sealed class WorkflowController(IWorkflowService service) : ControllerBase
{
    // ---- Definitions (admin designer) ----

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
    [HasPermission("Workflow.Edit")]
    public async Task<IActionResult> CreateDefinition([FromBody] CreateWorkflowDefinitionRequest req, CancellationToken ct)
    {
        var r = await service.CreateDefinitionAsync(req, ct);
        return r.Succeeded
            ? CreatedAtAction(nameof(Definition), new { id = r.Value!.Id }, r.Value)
            : BadRequest(new { error = r.Error });
    }

    // ---- Instances ----

    [HttpPost("instances")]
    [HasPermission("Workflow.Edit")]
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

    /// <summary>Resubmit a request that was returned for changes. No permission attribute: the service
    /// allows this to the requester alone, which is a narrower rule than any role could express.</summary>
    [HttpPost("instances/{id:long}/resubmit")]
    public async Task<IActionResult> Resubmit(long id, [FromBody] ResubmitRequest? req, CancellationToken ct)
    {
        var r = await service.ResubmitAsync(id, req?.Note, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    // ---- Worklist (the caller's own open tasks) ----

    [HttpGet("my-tasks")]
    public async Task<IActionResult> MyTasks(CancellationToken ct) => Ok(await service.MyWorklistAsync(ct));

    /// <summary>Approve / reject / return / request changes. Ownership of the task is the gate, checked
    /// in the service against the seat the caller occupies — a permission would be both too broad and
    /// too narrow for this.</summary>
    [HttpPost("tasks/{taskId:long}/actions")]
    public async Task<IActionResult> Act(long taskId, [FromBody] WorkflowActionRequest req, CancellationToken ct)
    {
        var r = await service.ActOnTaskAsync(taskId, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    // ---- Internal-mail integration ----

    /// <summary>The workflow strip for a piece of correspondence: which run it belongs to, whether this
    /// caller owes it an action, and the timeline. 204 when the mail is ordinary correspondence.</summary>
    [HttpGet("for-mail/{internalMailId:long}")]
    public async Task<IActionResult> ForMail(long internalMailId, CancellationToken ct)
    {
        var ctx = await service.ContextForInternalMailAsync(internalMailId, ct);
        return ctx is null ? NoContent() : Ok(ctx);
    }
}

/// <summary>Optional note explaining what the requester changed before resubmitting.</summary>
public sealed record ResubmitRequest(string? Note);
