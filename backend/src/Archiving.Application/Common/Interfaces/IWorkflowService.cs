using Archiving.Application.Common.Models;
using Archiving.Application.Features.Workflow;

namespace Archiving.Application.Common.Interfaces;

public interface IWorkflowService
{
    // Definitions
    Task<IReadOnlyList<WorkflowDefinitionListItem>> ListDefinitionsAsync(CancellationToken ct = default);
    Task<Result<WorkflowDefinitionDto>> GetDefinitionAsync(long id, CancellationToken ct = default);
    Task<Result<WorkflowDefinitionDto>> CreateDefinitionAsync(CreateWorkflowDefinitionRequest request, CancellationToken ct = default);

    // Instances
    Task<Result<WorkflowInstanceDto>> StartAsync(StartWorkflowRequest request, CancellationToken ct = default);
    Task<Result<WorkflowInstanceDto>> GetInstanceAsync(long id, CancellationToken ct = default);

    // Worklist (the caller's open tasks) + acting on a task
    Task<IReadOnlyList<WorklistItem>> MyWorklistAsync(CancellationToken ct = default);
    Task<Result<WorkflowInstanceDto>> ActOnTaskAsync(long taskId, WorkflowActionRequest request, CancellationToken ct = default);

    /// <summary>Starts the workflow configured for a newly composed internal mail, if any is. Returns
    /// null when no definition is bound to that mail's category — the ordinary case, and not an error.</summary>
    Task<WorkflowInstanceDto?> StartForInternalMailAsync(long internalMailId, long? mailCategoryId, CancellationToken ct = default);

    /// <summary>Puts a run parked on «إعادة للتعديل» back in motion after the requester has corrected
    /// the request. Only the requester may resubmit, and the instance — with everything already
    /// approved — is preserved.</summary>
    Task<Result<WorkflowInstanceDto>> ResubmitAsync(long instanceId, string? note, CancellationToken ct = default);

    /// <summary>The run attached to a piece of internal correspondence, with its timeline. Null when
    /// the correspondence is not part of a workflow.</summary>
    Task<WorkflowMailContextDto?> ContextForInternalMailAsync(long internalMailId, CancellationToken ct = default);

    /// <summary>Workflow columns for one page of the internal-mail list, keyed by mail id. Mails that
    /// are not part of a run are simply absent from the result.</summary>
    Task<IReadOnlyDictionary<long, WorkflowMailBadge>> BadgesForMailsAsync(
        IReadOnlyCollection<long> internalMailIds, CancellationToken ct = default);
}
