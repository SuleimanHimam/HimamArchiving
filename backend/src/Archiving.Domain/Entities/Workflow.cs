using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>A reusable, admin-defined multi-stage route (the heart of the system).</summary>
public class WorkflowDefinition : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TriggerModule { get; set; } = string.Empty;   // Document | InternalMail | IncomingMail | OutgoingMail
    public long? DocumentTypeId { get; set; }                   // dedicated workflow bound to a type (optional)

    /// <summary>Trigger key for <c>TriggerModule = "InternalMail"</c>: the تصنيف البريد that starts this
    /// run. Composing internal mail under this category starts the workflow with no further action from
    /// the sender. Null on a definition that is started explicitly rather than by a trigger.</summary>
    public long? MailCategoryId { get; set; }
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
    public bool RequiresCorrespondence { get; set; }           // on completion, raise an inter-dept memo if the next stage is another dept (Phase 2)

    // ---- Conditional & parallel routing (Phase 7) ----
    public StageType Type { get; set; } = StageType.Sequential;
    public string? ConditionExpression { get; set; }           // JSON: {"field":"Amount","operator":"greaterThan","value":100000}
    public int? ParallelGroupId { get; set; }                  // stages sharing this id run in parallel
    public bool RequiresAllParallelComplete { get; set; } = true; // true = AND join; false = OR join
    public DynamicAssignmentType AssignmentType { get; set; } = DynamicAssignmentType.Static;
    public string? AssignmentExpression { get; set; }          // JSON config for dynamic assignment

    // ---- Outcome rules ----
    /// <summary>What a rejection at this stage does to the run.</summary>
    public RejectBehavior RejectBehavior { get; set; } = RejectBehavior.StopAndReturnToRequester;

    /// <summary>Where the run resumes when the requester resubmits after a RequestChanges here.</summary>
    public ReturnResumeMode ReturnResume { get; set; } = ReturnResumeMode.ResumeAtSameStage;

    /// <summary>Quorum for a parallel group: how many of its stages must approve before the join
    /// releases ("2 of 3"). Null falls back to <see cref="RequiresAllParallelComplete"/> — all, or any
    /// one. Set on every stage of the group; the engine reads it from whichever it finds first.</summary>
    public int? MinimumApprovals { get; set; }

    /// <summary>
    /// The colour the administrator gave this stage on the canvas — a palette key ("violet",
    /// "amber", …), not a CSS value.
    ///
    /// A key rather than a hex code because the picture has to stay readable in both themes and on
    /// whatever the institution rebrands to: the client owns what each key paints, and a route drawn
    /// today does not freeze today's palette into the database. Null means the default tile, which is
    /// what every stage drawn before this existed keeps.
    /// </summary>
    public string? Color { get; set; }

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

    /// <summary>The internal correspondence the requester composed, and the root of the thread every
    /// generated message hangs from. Set when <see cref="EntityType"/> is <c>InternalMail</c>; it is the
    /// same value as <see cref="EntityId"/> then, but named because the routing reads it as a thread
    /// root rather than as the subject of the run.</summary>
    public long? RootInternalMailId { get; set; }

    /// <summary>Set while the run is parked on a RequestChanges: the stage that asked for them, so a
    /// resubmission can go back to exactly that desk.</summary>
    public long? ChangesRequestedByStageId { get; set; }

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

    /// <summary>The internal correspondence generated to put this task in front of its assignee. The
    /// task is the state; the mail is how the assignee meets it, in the inbox they already use.</summary>
    public long? InternalMailId { get; set; }

    public WorkflowInstance WorkflowInstance { get; set; } = null!;
    public WorkflowStage Stage { get; set; } = null!;
}
