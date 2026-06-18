using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Workflow;

// ---- Definitions ----

public sealed record WorkflowStageDto(
    long Id,
    string Name,
    int Order,
    string AssigneeType,
    long? AssigneePositionId,
    long? AssigneeOrgUnitId,
    long? AssigneeUserId,
    int ResponseHours,
    int? EscalateAfterHours,
    long? EscalateToPositionId,
    string AllowedActions,
    bool IsFinal);

public sealed record WorkflowDefinitionDto(
    long Id,
    string Name,
    string? Description,
    string TriggerModule,
    long? DocumentTypeId,
    int Version,
    bool IsActive,
    IReadOnlyList<WorkflowStageDto> Stages);

public sealed record WorkflowDefinitionListItem(
    long Id, string Name, string TriggerModule, int Version, bool IsActive, int StageCount);

public sealed record CreateWorkflowStageRequest(
    string Name,
    int Order,
    StageAssigneeType AssigneeType,
    long? AssigneePositionId,
    long? AssigneeOrgUnitId,
    long? AssigneeUserId,
    int ResponseHours,
    int? EscalateAfterHours,
    long? EscalateToPositionId,
    WorkflowActionType AllowedActions,
    bool IsFinal);

public sealed record CreateWorkflowDefinitionRequest(
    string Name,
    string? Description,
    string TriggerModule,
    long? DocumentTypeId,
    IReadOnlyList<CreateWorkflowStageRequest> Stages);

// ---- Instances & tasks ----

public sealed record StartWorkflowRequest(long WorkflowDefinitionId, string EntityType, long EntityId);

public sealed record WorkflowInstanceDto(
    long Id,
    long WorkflowDefinitionId,
    string DefinitionName,
    string EntityType,
    long EntityId,
    long? CurrentStageId,
    string? CurrentStageName,
    long? CurrentAssigneePositionId,
    string Status,
    DateTime StartedAt,
    DateTime? DueAt,
    DateTime? CompletedAt,
    IReadOnlyList<WorkflowTaskDto> Tasks);

public sealed record WorkflowTaskDto(
    long Id,
    long WorkflowInstanceId,
    long StageId,
    string StageName,
    long? AssignedToPositionId,
    long? AssignedToUserId,
    string Status,
    DateTime DueAt,
    string ActionTaken,
    string? Note,
    DateTime? CompletedAt,
    bool IsEscalated);

/// <summary>An open task in the caller's worklist, with the entity it concerns.</summary>
public sealed record WorklistItem(
    long TaskId,
    long WorkflowInstanceId,
    string EntityType,
    long EntityId,
    string DefinitionName,
    string StageName,
    string Status,
    DateTime DueAt,
    bool IsOverdue,
    string AllowedActions);

public sealed record WorkflowActionRequest(
    WorkflowActionType Action,
    string? Note,
    long? ForwardToPositionId);
