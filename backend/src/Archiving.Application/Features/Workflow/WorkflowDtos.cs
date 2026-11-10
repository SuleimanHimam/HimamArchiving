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
    bool IsFinal,
    // Phase 7 routing
    string Type = "Sequential",
    string? ConditionExpression = null,
    int? ParallelGroupId = null,
    bool RequiresAllParallelComplete = true,
    string AssignmentType = "Static",
    string? AssignmentExpression = null,
    bool RequiresCorrespondence = false,
    // Outcome rules
    string RejectBehavior = "StopAndReturnToRequester",
    string ReturnResume = "ResumeAtSameStage",
    int? MinimumApprovals = null,
    /// <summary>Palette key for the stage's tile on the canvas; null = the default tile.</summary>
    string? Color = null);

public sealed record WorkflowDefinitionDto(
    long Id,
    string Name,
    string? Description,
    string TriggerModule,
    long? DocumentTypeId,
    long? MailCategoryId,
    int Version,
    bool IsActive,
    IReadOnlyList<WorkflowStageDto> Stages);

public sealed record WorkflowDefinitionListItem(
    long Id, string Name, string TriggerModule, int Version, bool IsActive, int StageCount,
    long? MailCategoryId = null, string? MailCategoryName = null);

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
    bool IsFinal,
    // Phase 7 routing (optional; defaults preserve classic sequential behavior)
    StageType Type = StageType.Sequential,
    string? ConditionExpression = null,
    int? ParallelGroupId = null,
    bool RequiresAllParallelComplete = true,
    DynamicAssignmentType AssignmentType = DynamicAssignmentType.Static,
    string? AssignmentExpression = null,
    bool RequiresCorrespondence = false,
    // Outcome rules (defaults match the behaviour described in the admin designer)
    RejectBehavior RejectBehavior = Domain.Enums.RejectBehavior.StopAndReturnToRequester,
    ReturnResumeMode ReturnResume = ReturnResumeMode.ResumeAtSameStage,
    int? MinimumApprovals = null,
    string? Color = null);

public sealed record CreateWorkflowDefinitionRequest(
    string Name,
    string? Description,
    string TriggerModule,
    long? DocumentTypeId,
    IReadOnlyList<CreateWorkflowStageRequest> Stages,
    /// <summary>Trigger key when TriggerModule is "InternalMail": composing mail under this
    /// تصنيف البريد starts the run, with no action from the sender.</summary>
    long? MailCategoryId = null,
    bool IsActive = true);

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

// ---- Internal-mail integration ----

/// <summary>One entry in the run's timeline, as the participants would narrate it.</summary>
public sealed record WorkflowTimelineEntry(
    DateTime At,
    string Kind,               // Submitted | Sent | Approved | Rejected | ChangesRequested | Returned | Completed
    string StageName,
    string? ActorName,
    string? Note,
    long? InternalMailId);

/// <summary>Everything a piece of internal correspondence needs to show its workflow: the run, the
/// caller's own open task on it (if any), and the timeline. Null task means «للاطّلاع فقط».</summary>
public sealed record WorkflowMailContextDto(
    long InstanceId,
    long WorkflowDefinitionId,
    string DefinitionName,
    string Status,
    long RootInternalMailId,
    string? CurrentStageName,
    int StepNumber,
    int TotalSteps,
    string? MailRole,          // Origin | Step | Result
    bool ActionRequired,
    long? MyTaskId,
    string? MyTaskAllowedActions,
    DateTime? MyTaskDueAt,
    bool CanResubmit,
    IReadOnlyList<WorkflowTimelineEntry> Timeline);

/// <summary>The workflow columns an internal-mail row shows: which route it is on, where it has got
/// to, and whether this particular reader is the one holding it up. Returned keyed by mail id.</summary>
public sealed record WorkflowMailBadge(
    long InstanceId,
    string DefinitionName,
    string Status,
    string? CurrentStageName,
    int StepNumber,
    int TotalSteps,
    bool ActionRequired,
    long? MyTaskId,
    string? MailRole);
