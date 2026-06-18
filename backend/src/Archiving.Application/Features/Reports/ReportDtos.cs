namespace Archiving.Application.Features.Reports;

public sealed record StatusCount(string Status, int Count);

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
    IReadOnlyList<StatusCount> OutgoingByStatus);
