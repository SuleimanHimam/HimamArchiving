using Archiving.Application.Features.Reports;

namespace Archiving.Application.Common.Interfaces;

public interface IReportService
{
    Task<DashboardSummary> GetDashboardAsync(CancellationToken ct = default);

    // ---- Phase 10 structured reports ----
    Task<WorkflowPerformanceReport> GetWorkflowPerformanceAsync(DateTime? from, DateTime? to, long? definitionId, long? orgUnitId, CancellationToken ct = default);
    Task<DepartmentActivityReport> GetDepartmentActivityAsync(DateTime? from, DateTime? to, long? orgUnitId, CancellationToken ct = default);
    Task<DispositionStatusReport> GetDispositionStatusAsync(CancellationToken ct = default);
    Task<StorageUtilizationReport> GetStorageUtilizationAsync(CancellationToken ct = default);
    Task<TrendReport> GetTrendsAsync(string metric, DateTime? from, DateTime? to, string? granularity, CancellationToken ct = default);

    /// <summary>Per-bureau Diwan volumes plus real month-bucketed mail/attachment series for a calendar year.</summary>
    Task<DiwanDashboard> GetDiwanDashboardAsync(int? year, long? orgUnitId = null, CancellationToken ct = default);
}
