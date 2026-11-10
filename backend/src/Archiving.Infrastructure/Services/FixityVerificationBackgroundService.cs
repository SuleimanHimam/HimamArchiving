using Archiving.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Services;

/// <summary>Periodically re-verifies stored files against their ingest checksums (ISO 16363 fixity).
/// Cadence via <c>Fixity:IntervalMinutes</c> (default 1440), batch via <c>Fixity:BatchSize</c> (default 50).</summary>
public sealed class FixityVerificationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<FixityVerificationBackgroundService> logger) : BackgroundService
{
    /// <summary>Longest single wait handed to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
    private static readonly TimeSpan MaxDelaySlice = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = config.GetValue<int?>("Fixity:BatchSize") ?? 50;
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan interval;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var fixity = scope.ServiceProvider.GetRequiredService<IFixityService>();
                var failures = await fixity.SweepAsync(batch, stoppingToken);
                if (failures > 0) logger.LogWarning("Fixity sweep found {Failures} integrity failure(s)", failures);
                else logger.LogInformation("Fixity sweep completed with no failures");

                // Cadence: explicit config override wins (handy for dev); otherwise the preservation policy.
                var configMinutes = config.GetValue<int?>("Fixity:IntervalMinutes");
                if (configMinutes is { } cm)
                {
                    interval = TimeSpan.FromMinutes(Math.Max(1, cm));
                }
                else
                {
                    var policy = scope.ServiceProvider.GetRequiredService<IPreservationPolicyService>();
                    var days = (await policy.GetAsync(stoppingToken)).FixityCadenceDays;
                    interval = TimeSpan.FromDays(Math.Max(1, days));
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Fixity sweep failed"); interval = TimeSpan.FromMinutes(5); }

            // Task.Delay refuses anything over ~24.85 days (int.MaxValue ms), and a fixity cadence
            // of 30/90/365 days is an ordinary policy value — passing it straight in threw
            // ArgumentOutOfRangeException, which stopped the whole host (BackgroundServiceExceptionBehavior
            // defaults to StopHost). Long waits are therefore served in bounded slices.
            var due = DateTime.UtcNow + interval;
            var cancelled = false;
            while (!cancelled && DateTime.UtcNow < due)
            {
                var slice = due - DateTime.UtcNow;
                if (slice > MaxDelaySlice) slice = MaxDelaySlice;
                try { await Task.Delay(slice, stoppingToken); }
                catch (OperationCanceledException) { cancelled = true; }
            }
            if (cancelled) break;
        }
    }
}
