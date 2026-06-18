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
}
