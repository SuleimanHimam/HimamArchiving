namespace Archiving.Application.Features.Reports;

public sealed record StatusCount(string Status, int Count);

public sealed record AuditItem(
    long     Id,
    string   Action,
    string   EntityType,
    string   EntityTitle,
    DateTime CreatedAt,
    string?  UserName);

/// <summary>Aggregate counts for the dashboard, gated by the caller's clearance where relevant.</summary>
public sealed record DashboardSummary(
    int TotalDocuments,
    int TotalIncoming,
    int TotalOutgoing,
    int OpenWorkflowTasks,
    int OverdueWorkflowTasks,
    int ExpiringSoon,            // documents expiring within 30 days
    int PendingDisposals,
    int UnreadNotifications,
    IReadOnlyList<StatusCount> DocumentsByStatus,
    IReadOnlyList<StatusCount> IncomingByStatus,
    IReadOnlyList<StatusCount> OutgoingByStatus,
    IReadOnlyList<AuditItem>   RecentActivity);

// ---- Phase 10 structured reports ----

public sealed record WorkflowStagePerf(long StageId, string StageName, int Completed, double AvgMinutes);
public sealed record WorkflowPerformanceReport(
    double AvgCompletionMinutes, double SlaBreachRate, int CompletedCount, int RunningCount,
    IReadOnlyList<WorkflowStagePerf> ByStage);

public sealed record DepartmentActivityReport(
    long? OrgUnitId, int DocumentsCreated, int WorkflowsCompleted, int CorrespondenceSent, double AvgResponseMinutes);

public sealed record DispositionStatusReport(
    int PendingVerification, int PendingFinalApproval, int Approved, int Rejected, int Completed);

public sealed record DepartmentStorage(long OrgUnitId, string Name, long Bytes, int DocumentCount);
public sealed record StorageUtilizationReport(
    long TotalBytes, IReadOnlyList<DepartmentStorage> ByDepartment,
    int PhysicalSlotsTotal, int PhysicalSlotsUsed, int PhysicalSlotsAvailable);

public sealed record TrendPoint(DateTime Date, decimal Value);
public sealed record TrendReport(string Metric, string Granularity, IReadOnlyList<TrendPoint> DataPoints);

// ---- Diwan dashboard (per-bureau tiles + real monthly series) ----

/// <summary>Year-to-date incoming/outgoing totals for one receiving bureau (الى ديوان).</summary>
public sealed record BureauMailStat(long? OrgUnitId, string Name, int Incoming, int Outgoing);

/// <summary>One month of mail volume for one bureau — the stacked-bar series.</summary>
public sealed record MonthlyBureauPoint(int Month, long? OrgUnitId, int Incoming, int Outgoing);

/// <summary>Generic month-bucketed count (1-12).</summary>
public sealed record MonthlyCount(int Month, int Count);

/// <summary>One choice in the dashboard's department filter. Carried on the response rather than
/// fetched separately so the list is exactly what this caller is allowed to narrow to — and so it
/// does not collapse to the one department already selected on the next round trip.</summary>
public sealed record DashboardDepartment(long Id, string Name);

public sealed record DiwanDashboard(
    int Year,
    int TotalDocuments,
    IReadOnlyList<BureauMailStat>      Bureaus,
    IReadOnlyList<MonthlyBureauPoint>  MonthlyMail,
    IReadOnlyList<MonthlyCount>        MonthlyAttachments,
    IReadOnlyList<int>                 AvailableYears,
    /// <summary>Departments this caller may filter by — every one for an administrator, their own
    /// otherwise. Independent of the current selection, so the picker keeps its options.</summary>
    IReadOnlyList<DashboardDepartment> Departments = null!,
    /// <summary>The department the figures are narrowed to, or null for the whole institution.</summary>
    long? OrgUnitId = null);
