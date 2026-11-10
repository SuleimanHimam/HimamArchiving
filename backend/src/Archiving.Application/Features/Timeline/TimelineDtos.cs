namespace Archiving.Application.Features.Timeline;

/// <summary>One chronological entry in a document's unified timeline, normalized across all sources.</summary>
public sealed class TimelineEntryDto
{
    public long Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string EntryType { get; set; } = "";   // StatusChange | WorkflowAction | Correspondence | Share | FixityCheck | Escalation | Audit
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? ActorName { get; set; }
    public string? ActorPositionTitle { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}

public sealed record TimelineFilter(string? Types = null, DateTime? From = null, DateTime? To = null);
