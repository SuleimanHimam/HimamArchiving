using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Escalation;

public sealed record EscalationRuleDto(
    long Id, long? WorkflowDefinitionId, long? WorkflowStageId, int EscalationLevel, int OverdueMinutes,
    string Action, long? EscalateToPositionId, bool NotifyOriginator, bool IsActive);

public sealed record CreateEscalationRuleDto(
    long? WorkflowDefinitionId, long? WorkflowStageId, int EscalationLevel, int OverdueMinutes,
    EscalationAction Action, long? EscalateToPositionId, bool NotifyOriginator);

public sealed record EscalationLogDto(
    long Id, long WorkflowInstanceId, long WorkflowStageId, int EscalationLevel,
    long EscalatedToPositionId, string ActionTaken, DateTime EscalatedAt);
