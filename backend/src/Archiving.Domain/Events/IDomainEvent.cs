using MediatR;

namespace Archiving.Domain.Events;

/// <summary>
/// Marker for in-process domain events. Extends <see cref="INotification"/> so any number of
/// handlers can react without the publisher referencing them (decoupled cross-module workflow).
/// </summary>
public interface IDomainEvent : INotification
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
    /// <summary>Id of the user who triggered the change (string for transport neutrality); null for system jobs.</summary>
    string? TriggeredByUserId { get; }
}
