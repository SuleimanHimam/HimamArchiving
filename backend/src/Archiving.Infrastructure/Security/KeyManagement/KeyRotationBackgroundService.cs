using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Archiving.Infrastructure.Security.KeyManagement;

/// <summary>
/// Enforces the key rotation policy instead of documenting it.
///
/// <para>A rotation policy nobody executes is worth nothing at audit, and "rotate the master key" is
/// exactly the maintenance task that gets deferred forever when it means rewriting the archive. Here it
/// costs a table scan, so it runs unattended: once a day the sweep asks how old the oldest live KEK is
/// and rotates when it exceeds <c>Kms:RotationIntervalDays</c>.</para>
///
/// <para>Set <c>Kms:RotationIntervalDays</c> to 0 to disable the sweep where rotation is a change-managed
/// action — the on-demand endpoint still works, and the age is still reported.</para>
/// </summary>
public sealed class KeyRotationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<KmsOptions> options,
    ILogger<KeyRotationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalDays = options.Value.RotationIntervalDays;
        if (intervalDays <= 0)
        {
            logger.LogInformation("Scheduled KEK rotation is disabled (Kms:RotationIntervalDays = 0)");
            return;
        }

        // Let migrations and the KMS settle before touching key material.
        try { await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        do
        {
            try { await SweepAsync(intervalDays, stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "KEK rotation sweep failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task SweepAsync(int intervalDays, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rotation = scope.ServiceProvider.GetRequiredService<IKeyRotationService>();

        // Age of the oldest key material still in service: the most recent re-wrap, or the object's own
        // creation where it has never been re-wrapped.
        var oldest = await db.ArchivedObjects
            .Where(o => o.ShreddedAtUtc == null && o.WrappedDek != null)
            .Select(o => o.LastRewrappedAtUtc ?? o.CreatedAt)
            .OrderBy(t => t)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (oldest == default) return;   // nothing archived yet

        var age = DateTime.UtcNow - oldest;
        if (age < TimeSpan.FromDays(intervalDays))
        {
            logger.LogDebug("KEK rotation not due — oldest wrap is {Days:F1} days old", age.TotalDays);
            return;
        }

        logger.LogWarning("KEK rotation due — oldest wrap is {Days:F1} days old (policy {Policy} days)",
            age.TotalDays, intervalDays);

        var report = await rotation.RotateAsync(ct).ConfigureAwait(false);
        if (report.Failed > 0)
            logger.LogError("KEK rotation completed with {Failed} failures — inspect before retiring KEK versions",
                report.Failed);
    }
}
