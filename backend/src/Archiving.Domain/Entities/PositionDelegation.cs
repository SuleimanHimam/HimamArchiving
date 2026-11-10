using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>Temporary substitution: while a seat's holder is absent, their tasks route to a delegate seat.</summary>
public class PositionDelegation : BaseEntity
{
    public long FromPositionId { get; set; }          // the absent person's position
    public long ToPositionId { get; set; }             // the delegate's position
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DelegationScope Scope { get; set; } = DelegationScope.All;
    public long? WorkflowDefinitionId { get; set; }    // when Scope = SpecificWorkflowType
    public bool IsActive { get; set; } = true;
    public string? Reason { get; set; }                // "Annual leave", "Sick leave", …

    public Position FromPosition { get; set; } = null!;
    public Position ToPosition { get; set; } = null!;
}
