using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>A reusable, admin-defined multi-stage route (the heart of the system).</summary>
public class WorkflowDefinition : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TriggerModule { get; set; } = string.Empty;   // Document | IncomingMail | OutgoingMail
    public long? DocumentTypeId { get; set; }                   // dedicated workflow bound to a type (optional)
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    public ICollection<WorkflowStage> Stages { get; set; } = new List<WorkflowStage>();
}

/// <summary>One configurable step: responsible seat, response time, allowed actions, escalation.</summary>
public class WorkflowStage : BaseEntity
{
    public long WorkflowDefinitionId { get; set; }
    public string Name { get; set; } = string.Empty;           // e.g. Department Head, Manager, Archive
    public int Order { get; set; }

    public StageAssigneeType AssigneeType { get; set; } = StageAssigneeType.Position;
    public long? AssigneePositionId { get; set; }
    public long? AssigneeOrgUnitId { get; set; }
    public long? AssigneeRoleId { get; set; }
    public long? AssigneeUserId { get; set; }

    public int ResponseHours { get; set; } = 24;               // allowed execution duration
    public int? EscalateAfterHours { get; set; }               // null = use ResponseHours
    public long? EscalateToPositionId { get; set; }            // null = escalate to direct manager
    public WorkflowActionType AllowedActions { get; set; } = WorkflowActionType.Approve | WorkflowActionType.Reject | WorkflowActionType.Forward | WorkflowActionType.Comment;
    public string? TransitionCondition { get; set; }           // optional rule expression (JSON)
    public bool IsFinal { get; set; }

    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
}

/// <summary>A live run of a definition, bound to a document/mail entity. Tracks current location.</summary>
public class WorkflowInstance : BaseEntity
{
    public long WorkflowDefinitionId { get; set; }
    public string EntityType { get; set; } = string.Empty;     // Document | IncomingMail | OutgoingMail
    public long EntityId { get; set; }
    public long? CurrentStageId { get; set; }
    public long? CurrentAssigneePositionId { get; set; }       // "current location" of the transaction
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Running;
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? InitiatedByUserId { get; set; }

    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowStage? CurrentStage { get; set; }
    public ICollection<WorkflowTask> Tasks { get; set; } = new List<WorkflowTask>();
}

/// <summary>Per-stage assignment. Open tasks (CompletedAt == null) form a user's worklist;
/// completed tasks form the immutable routing history of the transaction.</summary>
public class WorkflowTask : BaseEntity
{
    public long WorkflowInstanceId { get; set; }
    public long StageId { get; set; }
    public long? AssignedToPositionId { get; set; }
    public long? AssignedToUserId { get; set; }                // resolved occupant at assignment time
    public WorkflowTaskStatus Status { get; set; } = WorkflowTaskStatus.Pending;
    public DateTime DueAt { get; set; }
    public WorkflowActionType ActionTaken { get; set; } = WorkflowActionType.None;
    public string? Note { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? CompletedByUserId { get; set; }
    public bool IsEscalated { get; set; }
    public DateTime? EscalatedAt { get; set; }

    public WorkflowInstance WorkflowInstance { get; set; } = null!;
    public WorkflowStage Stage { get; set; } = null!;
}
