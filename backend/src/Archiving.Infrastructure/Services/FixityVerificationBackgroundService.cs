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

            try { await Task.Delay(interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
