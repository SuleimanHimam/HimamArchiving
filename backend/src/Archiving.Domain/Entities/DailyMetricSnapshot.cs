using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

/// <summary>A daily metric value captured for trend analysis (Phase 10).</summary>
public class DailyMetricSnapshot : BaseEntity
{
    public DateTime Date { get; set; }
    public long? InstitutionId { get; set; }
    public long? OrgUnitId { get; set; }
    public string MetricType { get; set; } = string.Empty;   // WorkflowAvgCompletionMinutes | SLABreachCount | DocumentsByStatus | DispositionBacklog | StorageUsageBytes | NewDocuments | TasksCompleted
    public decimal Value { get; set; }
    public string? Breakdown { get; set; }                   // optional JSON for sub-metrics
}
