using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>A configurable, level-based rule for escalating overdue work up the hierarchy (Phase 6).</summary>
public class EscalationRule : BaseEntity
{
    public long? WorkflowDefinitionId { get; set; }  // null = applies globally
    public long? WorkflowStageId { get; set; }        // null = all stages in the definition
    public int EscalationLevel { get; set; }          // 1, 2, 3 … (sequential)
    public int OverdueMinutes { get; set; }           // fire this level once the task is this many minutes past SLA
    public EscalationAction Action { get; set; } = EscalationAction.NotifyOnly;
    public long? EscalateToPositionId { get; set; }   // explicit target; null = walk up the org hierarchy
    public bool NotifyOriginator { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Audit of each escalation that fired, so a level only fires once per task (Phase 6).</summary>
public class EscalationLog : BaseEntity
{
    public long WorkflowInstanceId { get; set; }
    public long WorkflowStageId { get; set; }
    public long EscalationRuleId { get; set; }
    public int EscalationLevel { get; set; }
    public long EscalatedToPositionId { get; set; }
    public EscalationAction ActionTaken { get; set; }
    public DateTime EscalatedAt { get; set; }
}
