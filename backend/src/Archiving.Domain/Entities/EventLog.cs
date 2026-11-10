namespace Archiving.Domain.Entities;

/// <summary>
/// Append-only record of every domain event that fired, for a complete event audit trail.
/// Written by the generic EventLogHandler. Standalone (not a BaseEntity) so it raises no events of its own.
/// </summary>
public sealed class EventLog
{
    public long Id { get; set; }
    public string EventType { get; set; } = default!;   // the event's CLR type name
    public string EventData { get; set; } = default!;   // serialized event payload (JSON)
    public string? TriggeredByUserId { get; set; }
    public DateTime OccurredAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
