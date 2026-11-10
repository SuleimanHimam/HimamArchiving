using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Reports;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class ReportService(AppDbContext db, ICurrentUser currentUser, IAccessScope scope) : IReportService
{
    /// <summary>
    /// The department a report may actually cover.
    ///
    /// The administrator reports on the institution and may ask for any department (or all of
    /// them, by passing none). Everyone else reports on their own department and nothing else:
    /// the requested id is ignored rather than honoured, because a department head asking for
    /// another department's numbers is asking for something they are not entitled to see.
    /// A manager covering several departments may ask for any one of *those*, and gets their own
    /// if they ask for something else.
    ///
    /// Returns null for "no restriction", and <c>NoDepartment</c> when the caller belongs to none —
    /// an id no department has, so the report comes back empty rather than institution-wide.
    /// </summary>
    private const long NoDepartment = -1;

    /// <summary>Stands in for "routed somewhere this caller is not part of" in the bureau
    /// breakdown. Negative so it can never collide with a real org-unit id.</summary>
    private const long OtherBureaus = -2;

    private async Task<long?> ReportScopeAsync(long? requested, CancellationToken ct)
    {
        if (await scope.IsSystemAdminAsync(ct)) return requested;
        var mine = await scope.OrgUnitIdsAsync(ct);
        if (mine.Count == 0) return NoDepartment;
        return requested is { } want && mine.Contains(want) ? want : mine[0];
    }

    /// <summary>Every department a report may cover, for the reports that do not take one.
    /// Null means the whole institution.</summary>
    private async Task<IReadOnlyList<long>?> ReportScopesAsync(CancellationToken ct)
    {
        if (await scope.IsSystemAdminAsync(ct)) return null;
        var mine = await scope.OrgUnitIdsAsync(ct);
        return mine.Count == 0 ? [NoDepartment] : mine;
    }

    /// <summary>Does the caller hold this permission code, through any of their roles?
    /// System Administrator holds everything, mirroring the API's own policy handler.</summary>
    private async Task<bool> HasPermissionAsync(string code, CancellationToken ct)
    {
        var me = currentUser.UserId;
        if (me is null) return false;
        return await db.UserRoles.AnyAsync(ur => ur.UserId == me && (
            db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "System Administrator")
            || db.RolePermissions.Any(rp => rp.RoleId == ur.RoleId
                && db.Permissions.Any(p => p.Id == rp.PermissionId && p.Code == code))), ct);
    }

    public async Task<DashboardSummary> GetDashboardAsync(CancellationToken ct = default)
    {
        var clearance = (int)currentUser.Clearance;
        var me = currentUser.UserId;
        var now = DateTime.UtcNow;
        var horizon = DateOnly.FromDateTime(now.AddDays(30));

        /* Department gate: non-admins' report counts cover only the departments they act in.
           This used to resolve the caller's role and department by hand, which meant it knew
           nothing about a department granted to a manager — their dashboard stayed narrower than
           every list screen they could open. IAccessScope is the one answer to that question. */
        var (isAdmin, depts, managed) = await scope.ResolveAsync(ct);

        // Clearance-gated base queries.
        var docs = db.Documents.Where(d => d.IsLatestVersion && (int)d.Confidentiality <= clearance);
        if (!isAdmin) docs = docs.VisibleDocuments(db, me, depts);
        var docsByStatus = await docs.GroupBy(d => d.Status)
            .Select(g => new StatusCount(g.Key.ToString(), g.Count())).ToListAsync(ct);
        // Correspondence-mail was removed; these axes are retained as empty for DTO stability.
        var incomingByStatus = new List<StatusCount>();
        var outgoingByStatus = new List<StatusCount>();

        /* Everything below answers "what is on MY desk", not "what is in the institution".
           The dashboard is the one screen every role opens, and it used to show org-wide
           totals to everyone: the count of every open task, every disposition request in
           flight, and a live feed of the last eight things anyone did — document titles
           included. Oversight figures now need the permission that grants oversight. */
        var openTaskStatuses = new[] { WorkflowTaskStatus.Pending, WorkflowTaskStatus.InProgress, WorkflowTaskStatus.Escalated };
        var tasks = db.WorkflowTasks.Where(t => openTaskStatuses.Contains(t.Status));
        // Workflow.View is what lets a head see their department's queue at all — it is not a
        // licence to count the institution's. Their own tasks plus the seats in their departments.
        if (!isAdmin)
            tasks = await HasPermissionAsync("Workflow.View", ct)
                ? tasks.VisibleTasks(db, me, depts)
                : tasks.Where(t => t.AssignedToUserId == me);
        var openTasks = await tasks.CountAsync(ct);
        var overdueTasks = await tasks.CountAsync(t => t.DueAt < now, ct);

        var expiringSoon = await docs.CountAsync(d => d.ExpiryDate != null && d.ExpiryDate <= horizon, ct);

        /* Disposition requests still in the two-step workflow, counted through the same rule the
           screen uses. Gating on the permission alone made the tile disagree with the page it
           links to: a department head was shown 139 requests waiting and then an empty list,
           because the number was the institution's and the list was their department's. */
        var disposals = db.DispositionRequests.Where(r =>
            r.Status == DispositionStatus.PendingVerification || r.Status == DispositionStatus.PendingFinalApproval);
        var pendingDisposals = await (await disposals.VisibleDispositionsAsync(db, scope, me, ct)).CountAsync(ct);

        var unread = me is null ? 0 : await db.Notifications.CountAsync(n => n.RecipientUserId == me && !n.IsRead, ct);

        /* The activity feed is the audit log in miniature, and it is narrowed the same way the log
           itself is: a department head sees their own staff's activity, everyone else sees their
           own trail. The permission alone was not enough — Audit.View is held by every manager,
           so the feed on the busiest screen in the system was showing each of them the whole
           institution's actions, document titles included. */
        var activity = db.AuditLogs.Where(a => !a.IsHidden);
        if (!isAdmin)
            activity = managed.Count == 0
                ? activity.Where(a => a.UserId == me)
                : activity.Where(a => a.UserId == me
                    || (a.UserId != null && db.Users.Any(u => u.Id == a.UserId
                        && u.OrgUnitId != null && managed.Contains(u.OrgUnitId.Value))));
        var recentActivity = await (
            from a in activity
            join u in db.Users.IgnoreQueryFilters() on a.UserId equals (long?)u.Id into uj
            from u in uj.DefaultIfEmpty()
            orderby a.CreatedAt descending
            select new AuditItem(a.Id, a.Action, a.EntityType ?? "", a.EntityTitle ?? "", a.CreatedAt, u.FullName)
        ).Take(8).ToListAsync(ct);

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
            OutgoingByStatus: outgoingByStatus,
            RecentActivity: recentActivity);
    }

    public async Task<WorkflowPerformanceReport> GetWorkflowPerformanceAsync(
        DateTime? from, DateTime? to, long? definitionId, long? orgUnitId, CancellationToken ct = default)
    {
        var instances = db.WorkflowInstances.AsQueryable();
        if (definitionId is { } d) instances = instances.Where(i => i.WorkflowDefinitionId == d);
        if (from is { } f) instances = instances.Where(i => i.StartedAt >= f);
        if (to is { } t) instances = instances.Where(i => i.StartedAt <= t);

        var completed = await instances.Where(i => i.Status == WorkflowStatus.Completed && i.CompletedAt != null)
            .Select(i => new { i.StartedAt, i.CompletedAt }).ToListAsync(ct);
        var runningCount = await instances.CountAsync(i => i.Status == WorkflowStatus.Running, ct);

        var avgCompletion = completed.Count > 0
            ? completed.Average(i => (i.CompletedAt!.Value - i.StartedAt).TotalMinutes) : 0;

        var instIds = await instances.Select(i => i.Id).ToListAsync(ct);
        var tasks = await db.WorkflowTasks.Where(tk => instIds.Contains(tk.WorkflowInstanceId) && tk.CompletedAt != null)
            .Select(tk => new { tk.StageId, Stage = tk.Stage.Name, tk.CreatedAt, tk.CompletedAt, tk.IsEscalated }).ToListAsync(ct);
        var slaBreachRate = tasks.Count > 0 ? (double)tasks.Count(x => x.IsEscalated) / tasks.Count : 0;

        var byStage = tasks.GroupBy(x => new { x.StageId, x.Stage })
            .Select(g => new WorkflowStagePerf(g.Key.StageId, g.Key.Stage, g.Count(),
                g.Average(x => (x.CompletedAt!.Value - x.CreatedAt).TotalMinutes)))
            .ToList();

        return new WorkflowPerformanceReport(Math.Round(avgCompletion, 1), Math.Round(slaBreachRate, 3), completed.Count, runningCount, byStage);
    }

    public async Task<DepartmentActivityReport> GetDepartmentActivityAsync(
        DateTime? from, DateTime? to, long? orgUnitId, CancellationToken ct = default)
    {
        orgUnitId = await ReportScopeAsync(orgUnitId, ct);
        var docs = db.Documents.Where(d => d.IsLatestVersion);
        if (orgUnitId is { } ou) docs = docs.Where(d => d.OwningOrgUnitId == ou);
        if (from is { } f) docs = docs.Where(d => d.CreatedAt >= f);
        if (to is { } t) docs = docs.Where(d => d.CreatedAt <= t);
        var docsCreated = await docs.CountAsync(ct);

        var wfDone = db.WorkflowInstances.Where(i => i.Status == WorkflowStatus.Completed);
        if (from is { } f2) wfDone = wfDone.Where(i => i.CompletedAt >= f2);
        if (to is { } t2) wfDone = wfDone.Where(i => i.CompletedAt <= t2);
        var workflowsCompleted = await wfDone.CountAsync(ct);

        // Correspondence-mail was removed; these metrics are retained as zero for DTO stability.
        return new DepartmentActivityReport(orgUnitId, docsCreated, workflowsCompleted, 0, 0);
    }

    public async Task<DispositionStatusReport> GetDispositionStatusAsync(CancellationToken ct = default)
    {
        // Same rule as the disposal screen: a head reports on their departments' requests, not on
        // the institution's disposal history.
        var visible = await db.DispositionRequests.VisibleDispositionsAsync(db, scope, currentUser.UserId, ct);
        var g = await visible.GroupBy(r => r.Status)
            .Select(x => new { Status = x.Key, Count = x.Count() }).ToListAsync(ct);
        int C(DispositionStatus s) => g.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        return new DispositionStatusReport(
            C(DispositionStatus.PendingVerification), C(DispositionStatus.PendingFinalApproval),
            C(DispositionStatus.Approved), C(DispositionStatus.Rejected), C(DispositionStatus.Completed));
    }

    public async Task<StorageUtilizationReport> GetStorageUtilizationAsync(CancellationToken ct = default)
    {
        // A department head sees their own department's consumption, not a league table of every
        // department in the institution.
        var only = await ReportScopesAsync(ct);

        var attachments = db.DocumentAttachments.AsQueryable();
        if (only is { } mine)
            attachments = attachments.Where(a => db.Documents.Any(d => d.Id == a.DocumentId && mine.Contains(d.OwningOrgUnitId)));
        var totalBytes = await attachments.SumAsync(a => (long?)a.SizeBytes, ct) ?? 0;

        var docsQuery = db.Documents.Where(d => d.IsLatestVersion);
        if (only is { } depts) docsQuery = docsQuery.Where(d => depts.Contains(d.OwningOrgUnitId));

        var byDept = await docsQuery
            .GroupBy(d => d.OwningOrgUnitId)
            .Select(grp => new
            {
                OrgUnitId = grp.Key,
                DocumentCount = grp.Count(),
                Bytes = grp.SelectMany(d => d.Attachments).Sum(a => (long?)a.SizeBytes) ?? 0,
            })
            .ToListAsync(ct);
        var names = await db.OrgUnits.ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var deptStorage = byDept
            .Select(x => new DepartmentStorage(x.OrgUnitId, names.GetValueOrDefault(x.OrgUnitId, "—"), x.Bytes, x.DocumentCount))
            .OrderByDescending(x => x.Bytes).ToList();

        var slotsTotal = await db.Boxes.SumAsync(box => (int?)(box.Capacity ?? 0), ct) ?? 0;
        var slotsUsed = await db.Boxes.SumAsync(box => (int?)box.CurrentCount, ct) ?? 0;

        return new StorageUtilizationReport(totalBytes, deptStorage, slotsTotal, slotsUsed, Math.Max(0, slotsTotal - slotsUsed));
    }

    public async Task<TrendReport> GetTrendsAsync(
        string metric, DateTime? from, DateTime? to, string? granularity, CancellationToken ct = default)
    {
        var q = db.DailyMetricSnapshots.Where(s => s.MetricType == metric);
        if (from is { } f) q = q.Where(s => s.Date >= f);
        if (to is { } t) q = q.Where(s => s.Date <= t);
        var rows = await q.OrderBy(s => s.Date).Select(s => new { s.Date, s.Value }).ToListAsync(ct);

        var gran = (granularity ?? "daily").ToLowerInvariant();
        var points = gran == "monthly"
            ? rows.GroupBy(r => new DateTime(r.Date.Year, r.Date.Month, 1))
                  .Select(grp => new TrendPoint(grp.Key, grp.Sum(x => x.Value))).ToList()
            : rows.Select(r => new TrendPoint(r.Date, r.Value)).ToList();

        return new TrendReport(metric, gran, points);
    }

    public async Task<DiwanDashboard> GetDiwanDashboardAsync(int? year, long? orgUnitId = null, CancellationToken ct = default)
    {
        var clearance = (int)currentUser.Clearance;
        var y = year ?? DateTime.UtcNow.Year;
        var (isAdmin, depts, managed) = await scope.ResolveAsync(ct);
        var me = currentUser.UserId;

        /* Clearance-gated base set; MailDate is the registry date the bureaus actually file by.
         *
         * The department gate matters as much as the clearance one, and was missing: these charts
         * counted every letter in the institution for every signed-in user, so an employee's
         * dashboard reported traffic they cannot open, broken down by departments they are not in.
         * The predicate is the register's own — see ScopeFilters. */
        var mail = db.DiwanMails.AsNoTracking().Where(m => (int)m.Classification <= clearance);
        if (!isAdmin) mail = mail.VisibleDiwan(db, me, managed);

        // Years that actually hold mail — drives the year picker instead of a hardcoded range.
        var availableYears = await mail
            .Select(m => m.MailDate.Year).Distinct().OrderByDescending(v => v).ToListAsync(ct);
        if (availableYears.Count == 0) availableYears.Add(y);

        /* The department filter.
         *
         * Asked for, and refused, on the caller's own terms: an administrator may narrow to any
         * department, anyone else only to one they are already in. A request for a department
         * outside that set is dropped rather than answered with someone else's traffic — the
         * clearance and routing gates above decide what is visible, and a query string must not
         * talk its way past them.
         */
        var filterUnit = orgUnitId is { } requested && (isAdmin || depts.Contains(requested))
            ? requested
            : (long?)null;

        var inYear = mail.Where(m => m.MailDate.Year == y);
        // Narrowed to the letters that actually reached this department, which is also what makes
        // the attachment series below agree with the bars above it.
        if (filterUnit is { } only)
            inYear = inYear.Where(m => m.ToDiwanOrgUnits.Any(b => b.OrgUnitId == only));

        // A letter can be routed to several bureaus, so it is counted once per bureau it reaches.
        // Bureau totals therefore sum to more than the letter count when routing overlaps — that is
        // intended: each bureau's figure is the volume that bureau actually handled.
        // Letters with no routing collapse into a single "unassigned" bucket (null).
        /* A visible letter may also have been routed to a department the caller has nothing to do
           with. Those segments still count — dropping them would make the chart disagree with the
           register — but they are not named: a legend listing every department in the institution
           told an employee which units handle what, which is not their chart to read. They fold
           into one «أقسام أخرى» band instead. */
        var routed = inYear
            .SelectMany(m => m.ToDiwanOrgUnits
                // Under a department filter only that department's segments are counted: a letter
                // it shares with another bureau still belongs to the other bureau's chart, not this
                // one, and stacking both would answer a question nobody asked.
                .Where(b => filterUnit == null || b.OrgUnitId == filterUnit)
                .Select(b => new
                {
                    OrgUnitId = isAdmin || depts.Contains(b.OrgUnitId) ? (long?)b.OrgUnitId : OtherBureaus,
                    Month = m.MailDate.Month,
                    m.Direction,
                }));
        // A letter routed nowhere reached no department, so it has no place in a filtered chart.
        var unrouted = inYear
            .Where(m => filterUnit == null && !m.ToDiwanOrgUnits.Any())
            .Select(m => new { OrgUnitId = (long?)null, Month = m.MailDate.Month, m.Direction });

        var buckets = await routed.Concat(unrouted)
            .GroupBy(x => new { x.OrgUnitId, x.Month, x.Direction })
            .Select(g => new
            {
                ToDiwanOrgUnitId = g.Key.OrgUnitId,
                g.Key.Month,
                g.Key.Direction,
                Count = g.Count(),
            })
            .ToListAsync(ct);

        // Resolve bureau names in one round-trip; unassigned mail folds into a single bucket.
        var bureauIds = buckets.Where(b => b.ToDiwanOrgUnitId != null)
            .Select(b => b.ToDiwanOrgUnitId!.Value).Distinct().ToList();
        var names = await db.OrgUnits.AsNoTracking()
            .Where(o => bureauIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Name, ct);

        // null = the letter was never routed anywhere; OtherBureaus = it went somewhere the caller
        // is not part of. Two different facts, so two different labels.
        string NameOf(long? id) =>
            id == OtherBureaus ? "أقسام أخرى"
            : id is { } v && names.TryGetValue(v, out var n) ? n : "غير محدد";

        var bureaus = buckets
            .GroupBy(b => b.ToDiwanOrgUnitId)
            .Select(g => new BureauMailStat(
                g.Key,
                NameOf(g.Key),
                g.Where(x => x.Direction == DiwanDirection.Incoming).Sum(x => x.Count),
                g.Where(x => x.Direction == DiwanDirection.Outgoing).Sum(x => x.Count)))
            .OrderByDescending(b => b.Incoming + b.Outgoing)
            .ToList();

        var monthlyMail = buckets
            .GroupBy(b => new { b.ToDiwanOrgUnitId, b.Month })
            .Select(g => new MonthlyBureauPoint(
                g.Key.Month,
                g.Key.ToDiwanOrgUnitId,
                g.Where(x => x.Direction == DiwanDirection.Incoming).Sum(x => x.Count),
                g.Where(x => x.Direction == DiwanDirection.Outgoing).Sum(x => x.Count)))
            .OrderBy(p => p.Month)
            .ToList();

        /* Attachments are dated by their parent mail so the two charts share one time axis — and
           are drawn from the same visible set, so the two charts also agree on whose mail they
           are describing. Counting them independently made the attachment bars taller than the
           letters they hang off. */
        var visibleIds = inYear.Select(m => m.Id);
        var attachRows = await db.DiwanAttachments.AsNoTracking()
            .Where(a => visibleIds.Contains(a.DiwanMailId))
            .GroupBy(a => a.DiwanMail.MailDate.Month)
            .Select(g => new { Month = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Zero-fill both series so the charts always render a full 12-month axis.
        var attachByMonth = attachRows.ToDictionary(r => r.Month, r => r.Count);
        var monthlyAttachments = Enumerable.Range(1, 12)
            .Select(m => new MonthlyCount(m, attachByMonth.TryGetValue(m, out var c) ? c : 0))
            .ToList();

        var docsQ = db.Documents.Where(d => d.IsLatestVersion && (int)d.Confidentiality <= clearance);
        if (!await scope.SeesAllDocumentsAsync(ct)) docsQ = docsQ.VisibleDocuments(db, me, depts);
        if (filterUnit is { } owner) docsQ = docsQ.Where(d => d.OwningOrgUnitId == owner);
        var totalDocuments = await docsQ.CountAsync(ct);

        // The picker's options: every active department for an administrator, the caller's own
        // otherwise. Read from the organisation rather than from this year's traffic, so choosing a
        // quiet department does not empty the list that would let you choose another.
        var departments = await db.OrgUnits.AsNoTracking()
            .Where(o => o.IsActive && (isAdmin || depts.Contains(o.Id)))
            .OrderBy(o => o.SortOrder).ThenBy(o => o.Name)
            .Select(o => new DashboardDepartment(o.Id, o.Name))
            .ToListAsync(ct);

        return new DiwanDashboard(y, totalDocuments, bureaus, monthlyMail, monthlyAttachments,
            availableYears, departments, filterUnit);
    }
}
