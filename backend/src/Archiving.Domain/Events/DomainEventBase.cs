namespace Archiving.Domain.Events;

/// <summary>Base for all domain events: stamps a unique id, the UTC occurrence time, and the triggering user.</summary>
public abstract record DomainEventBase : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string? TriggeredByUserId { get; init; }
}
