using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Services;

/// <summary>Captures daily KPI snapshots for trend analysis (Phase 10). Cadence via
/// <c>Metrics:IntervalSeconds</c> (default 86400 = daily); idempotent per UTC day.</summary>
public sealed class MetricsSnapshotBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<MetricsSnapshotBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = config.GetValue<int?>("Metrics:IntervalSeconds") ?? 86_400;
        var interval = TimeSpan.FromSeconds(Math.Max(60, seconds));
        try { await Task.Delay(TimeSpan.FromSeconds(40), stoppingToken); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(interval);
        do
        {
            try { await SnapshotAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Metrics snapshot failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SnapshotAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = DateTime.UtcNow.Date;
        var dayStart = today;
        var dayEnd = today.AddDays(1);

        // Idempotent: skip if today's snapshot already exists.
        if (await db.DailyMetricSnapshots.AnyAsync(s => s.Date == today, ct)) return;

        async Task Add(string metric, decimal value) =>
            db.DailyMetricSnapshots.Add(new DailyMetricSnapshot { Date = today, MetricType = metric, Value = value });

        await Add("DocumentsTotal", await db.Documents.CountAsync(d => d.IsLatestVersion, ct));
        await Add("NewDocuments", await db.Documents.CountAsync(d => d.CreatedAt >= dayStart && d.CreatedAt < dayEnd, ct));
        await Add("SLABreachCount", await db.EscalationLogs.CountAsync(e => e.EscalatedAt >= dayStart && e.EscalatedAt < dayEnd, ct));
        await Add("DispositionBacklog", await db.DispositionRequests.CountAsync(
            r => r.Status == DispositionStatus.PendingVerification || r.Status == DispositionStatus.PendingFinalApproval, ct));
        await Add("TasksCompleted", await db.WorkflowTasks.CountAsync(t => t.CompletedAt >= dayStart && t.CompletedAt < dayEnd, ct));
        await Add("StorageUsageBytes", await db.DocumentAttachments.SumAsync(a => (long?)a.SizeBytes, ct) ?? 0);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Captured daily metric snapshot for {Date:yyyy-MM-dd}", today);
    }
}
