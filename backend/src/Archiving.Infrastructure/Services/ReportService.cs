using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Reports;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class ReportService(AppDbContext db, ICurrentUser currentUser) : IReportService
{
    public async Task<DashboardSummary> GetDashboardAsync(CancellationToken ct = default)
    {
        var clearance = (int)currentUser.Clearance;
        var me = currentUser.UserId;
        var now = DateTime.UtcNow;
        var horizon = DateOnly.FromDateTime(now.AddDays(30));

        // Clearance-gated base queries.
        var docs = db.Documents.Where(d => d.IsLatestVersion && (int)d.Confidentiality <= clearance);
        var incoming = db.IncomingMails.Where(m => (int)m.Confidentiality <= clearance);
        var outgoing = db.OutgoingMails.Where(m => (int)m.Confidentiality <= clearance);

        var docsByStatus = await docs.GroupBy(d => d.Status)
            .Select(g => new StatusCount(g.Key.ToString(), g.Count())).ToListAsync(ct);
        var incomingByStatus = await incoming.GroupBy(m => m.Status)
            .Select(g => new StatusCount(g.Key.ToString(), g.Count())).ToListAsync(ct);
        var outgoingByStatus = await outgoing.GroupBy(m => m.Status)
            .Select(g => new StatusCount(g.Key.ToString(), g.Count())).ToListAsync(ct);

        var openTaskStatuses = new[] { WorkflowTaskStatus.Pending, WorkflowTaskStatus.InProgress, WorkflowTaskStatus.Escalated };
        var openTasks = await db.WorkflowTasks.CountAsync(t => openTaskStatuses.Contains(t.Status), ct);
        var overdueTasks = await db.WorkflowTasks
            .CountAsync(t => openTaskStatuses.Contains(t.Status) && t.DueAt < now, ct);

        var expiringSoon = await docs.CountAsync(d => d.ExpiryDate != null && d.ExpiryDate <= horizon, ct);
        var pendingDisposals = await db.DisposalRequests
            .CountAsync(r => r.Status == DisposalRequestStatus.Pending, ct);
        var unread = me is null ? 0 : await db.Notifications.CountAsync(n => n.RecipientUserId == me && !n.IsRead, ct);

        return new DashboardSummary(
            TotalDocuments: docsByStatus.Sum(s => s.Count),
            TotalIncoming: incomingByStatus.Sum(s => s.Count),
            TotalOutgoing: outgoingByStatus.Sum(s => s.Count),
            OpenWorkflowTasks: openTasks,
            OverdueWorkflowTasks: overdueTasks,
            ExpiringSoon: expiringSoon,
            PendingDisposals: pendingDisposals,
            UnreadNotifications: unread,
            DocumentsByStatus: docsByStatus,
            IncomingByStatus: incomingByStatus,
            OutgoingByStatus: outgoingByStatus);
    }
}
