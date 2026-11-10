using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Archiving.Infrastructure.Services;

/// <summary>Delivers queued <c>WebhookDelivery</c> rows to their hook's callback URL with an optional
/// HMAC-SHA256 signature, retrying with exponential backoff up to the hook's MaxRetries (Phase 11).
/// Poll cadence via <c>Webhooks:IntervalSeconds</c> (default 30).</summary>
public sealed class WebhookDeliveryBackgroundService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<WebhookDeliveryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = config.GetValue<int?>("Webhooks:IntervalSeconds") ?? 30;
        var interval = TimeSpan.FromSeconds(Math.Max(5, seconds));
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); } catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(interval);
        do
        {
            try { await ProcessAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Webhook delivery sweep failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var due = await db.WebhookDeliveries
            .Where(d => (d.Status == DeliveryStatus.Pending || d.Status == DeliveryStatus.Retrying)
                     && (d.NextAttemptAt == null || d.NextAttemptAt <= now))
            .OrderBy(d => d.Id).Take(20).ToListAsync(ct);
        if (due.Count == 0) return;

        var client = httpClientFactory.CreateClient("webhooks");
        foreach (var delivery in due)
        {
            var hook = await db.IntegrationHooks.FirstOrDefaultAsync(h => h.Id == delivery.IntegrationHookId, ct);
            if (hook is null || !hook.IsActive) { delivery.Status = DeliveryStatus.Failed; delivery.ErrorMessage = "hook missing/inactive"; continue; }

            delivery.AttemptNumber += 1;
            try
            {
                using var req = new HttpRequestMessage(
                    new HttpMethod(string.IsNullOrWhiteSpace(hook.HttpMethod) ? "POST" : hook.HttpMethod!), hook.CallbackUrl)
                {
                    Content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json"),
                };
                req.Headers.TryAddWithoutValidation("X-Event-Type", delivery.EventType);
                ApplyCustomHeaders(req, hook.HeadersJson);
                if (!string.IsNullOrWhiteSpace(hook.SecretKey))
                    req.Headers.TryAddWithoutValidation("X-Signature-256", "sha256=" + Sign(delivery.PayloadJson, hook.SecretKey!));

                using var resp = await client.SendAsync(req, ct);
                delivery.HttpStatusCode = (int)resp.StatusCode;
                delivery.ResponseBody = Truncate(await resp.Content.ReadAsStringAsync(ct), 2000);

                if (resp.IsSuccessStatusCode)
                {
                    delivery.Status = DeliveryStatus.Delivered;
                    delivery.DeliveredAt = DateTime.UtcNow;
                    delivery.ErrorMessage = null;
                }
                else
                {
                    Reschedule(delivery, hook, $"HTTP {(int)resp.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Reschedule(delivery, hook, ex.Message);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static void Reschedule(Domain.Entities.WebhookDelivery d, Domain.Entities.IntegrationHook hook, string error)
    {
        d.ErrorMessage = error;
        if (d.AttemptNumber >= Math.Max(1, hook.MaxRetries))
        {
            d.Status = DeliveryStatus.Failed;
        }
        else
        {
            d.Status = DeliveryStatus.Retrying;
            var backoff = hook.RetryDelaySeconds * Math.Pow(2, d.AttemptNumber - 1);
            d.NextAttemptAt = DateTime.UtcNow.AddSeconds(backoff);
        }
    }

    private static void ApplyCustomHeaders(HttpRequestMessage req, string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson)) return;
        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
            if (headers is null) return;
            foreach (var (k, v) in headers) req.Headers.TryAddWithoutValidation(k, v);
        }
        catch { /* malformed header config is ignored */ }
    }

    private static string Sign(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private static string? Truncate(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}
