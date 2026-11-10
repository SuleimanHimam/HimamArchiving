namespace Archiving.Application.Features.Tasks;

/// <summary>A single actionable item, normalized across every module (workflow, mail, disposition, …).</summary>
public sealed class UnifiedTaskDto
{
    public string TaskId { get; set; } = "";          // e.g. "workflow-123", "mail-456"
    public string TaskType { get; set; } = "";          // WorkflowStage | IncomingMail | DispositionReview | CorrespondenceResponse | SharedDocument | ReturnedDocument
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Priority { get; set; } = "Normal";    // High | Normal | Low
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public long? DocumentId { get; set; }
    public string? DocumentTitle { get; set; }
    public string SourceModule { get; set; } = "";      // Workflow | Mail | Disposition | Correspondence | Documents
    public List<string> AvailableActions { get; set; } = [];
    public string? AssignedVia { get; set; }            // Direct | Delegation | Escalation
}

/// <summary>Optional filters for the unified worklist.</summary>
public sealed record UnifiedTaskFilter(
    string? TaskType = null,
    string? Priority = null,
    bool? IsOverdue = null,
    string? SourceModule = null,
    DateTime? From = null,
    DateTime? To = null);

/// <summary>Per-type counts plus the overdue total, for badges.</summary>
public sealed class TaskCountsDto
{
    public int Total { get; set; }
    public int Overdue { get; set; }
    public Dictionary<string, int> ByType { get; set; } = [];
}
