using Archiving.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Services;

/// <summary>
/// Extracts searchable text from newly ingested (and backfilled) attachments in the background.
/// OCR is slow, so this runs out-of-band. Config: <c>Search:IndexingEnabled</c> (default true),
/// <c>Search:BatchSize</c> (default 5), <c>Search:IdleSeconds</c> (default 60).
/// </summary>
public sealed class TextExtractionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<TextExtractionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Search:IndexingEnabled", true)) return;

        var batch = Math.Max(1, config.GetValue("Search:BatchSize", 5));
        var idle = TimeSpan.FromSeconds(Math.Max(5, config.GetValue("Search:IdleSeconds", 60)));

        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                using var scope = scopeFactory.CreateScope();
                var indexer = scope.ServiceProvider.GetRequiredService<ITextIndexingService>();
                processed = await indexer.SweepPendingAsync(batch, stoppingToken);
                if (processed > 0) logger.LogInformation("Text extraction processed {Count} attachment(s)", processed);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Text extraction sweep failed"); }

            // Drain quickly while there's a full batch of work; otherwise idle.
            var delay = processed >= batch ? TimeSpan.FromSeconds(2) : idle;
            try { await Task.Delay(delay, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
