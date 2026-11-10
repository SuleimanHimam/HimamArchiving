using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Services;

/// <summary>Daily job that deactivates position delegations whose end date has passed.
/// Cadence via <c>Delegation:IntervalSeconds</c> (default 86400 = daily).</summary>
public sealed class DelegationExpiryBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<DelegationExpiryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = config.GetValue<int?>("Delegation:IntervalSeconds") ?? 86_400;
        var interval = TimeSpan.FromSeconds(Math.Max(60, seconds));
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(interval);
        do
        {
            try { await SweepAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Delegation expiry sweep failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var expired = await db.PositionDelegations.Where(d => d.IsActive && d.EndDate < now).ToListAsync(ct);
        if (expired.Count == 0) return;

        foreach (var d in expired) d.IsActive = false;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Deactivated {Count} expired delegation(s)", expired.Count);
    }
}
