using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Integrations;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class IntegrationHookService(AppDbContext db, IAuditWriter audit) : IIntegrationHookService
{
    public async Task<IReadOnlyList<IntegrationHookDto>> ListAsync(CancellationToken ct = default) =>
        await db.IntegrationHooks.OrderBy(h => h.Name).Select(h => Map(h)).ToListAsync(ct);

    public async Task<Result<IntegrationHookDto>> CreateAsync(CreateIntegrationHookDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.EventType) || string.IsNullOrWhiteSpace(dto.CallbackUrl))
            return Result<IntegrationHookDto>.Fail("الاسم ونوع الحدث وعنوان الاستدعاء مطلوبة");
        if (!Uri.TryCreate(dto.CallbackUrl, UriKind.Absolute, out _))
            return Result<IntegrationHookDto>.Fail("عنوان الاستدعاء غير صالح");

        var h = new IntegrationHook
        {
            Name = dto.Name, EventType = dto.EventType, CallbackUrl = dto.CallbackUrl,
            HttpMethod = string.IsNullOrWhiteSpace(dto.HttpMethod) ? "POST" : dto.HttpMethod,
            HeadersJson = dto.HeadersJson, SecretKey = dto.SecretKey,
            MaxRetries = dto.MaxRetries <= 0 ? 3 : dto.MaxRetries,
            RetryDelaySeconds = dto.RetryDelaySeconds <= 0 ? 60 : dto.RetryDelaySeconds,
            InstitutionId = dto.InstitutionId, IsActive = true,
        };
        db.IntegrationHooks.Add(h);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("IntegrationHookCreated", "IntegrationHook", h.Id, h.Name, ct: ct);
        return Result<IntegrationHookDto>.Ok(Map(h));
    }

    public async Task<Result<IntegrationHookDto>> UpdateAsync(long id, CreateIntegrationHookDto dto, CancellationToken ct = default)
    {
        var h = await db.IntegrationHooks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (h is null) return Result<IntegrationHookDto>.Fail("الاشتراك غير موجود");
        h.Name = dto.Name; h.EventType = dto.EventType; h.CallbackUrl = dto.CallbackUrl;
        h.HttpMethod = string.IsNullOrWhiteSpace(dto.HttpMethod) ? "POST" : dto.HttpMethod;
        h.HeadersJson = dto.HeadersJson;
        if (!string.IsNullOrWhiteSpace(dto.SecretKey)) h.SecretKey = dto.SecretKey;   // keep existing secret if blank
        h.MaxRetries = dto.MaxRetries <= 0 ? 3 : dto.MaxRetries;
        h.RetryDelaySeconds = dto.RetryDelaySeconds <= 0 ? 60 : dto.RetryDelaySeconds;
        h.InstitutionId = dto.InstitutionId;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("IntegrationHookUpdated", "IntegrationHook", h.Id, h.Name, ct: ct);
        return Result<IntegrationHookDto>.Ok(Map(h));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var h = await db.IntegrationHooks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (h is null) return Result<bool>.Fail("الاشتراك غير موجود");
        db.IntegrationHooks.Remove(h);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("IntegrationHookDeleted", "IntegrationHook", id, ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<Result<bool>> TestAsync(long id, CancellationToken ct = default)
    {
        var h = await db.IntegrationHooks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (h is null) return Result<bool>.Fail("الاشتراك غير موجود");

        db.WebhookDeliveries.Add(new WebhookDelivery
        {
            IntegrationHookId = h.Id,
            EventType = h.EventType,
            PayloadJson = $$"""{"test":true,"hookId":{{h.Id}},"eventType":"{{h.EventType}}","at":"{{DateTime.UtcNow:O}}"}""",
            Status = DeliveryStatus.Pending,
            NextAttemptAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    public async Task<IReadOnlyList<WebhookDeliveryDto>> GetDeliveriesAsync(long hookId, CancellationToken ct = default) =>
        await db.WebhookDeliveries.Where(d => d.IntegrationHookId == hookId)
            .OrderByDescending(d => d.Id).Take(100)
            .Select(d => new WebhookDeliveryDto(d.Id, d.IntegrationHookId, d.EventType, d.AttemptNumber, d.HttpStatusCode,
                d.Status.ToString(), d.DeliveredAt, d.NextAttemptAt, d.ErrorMessage, d.CreatedAt))
            .ToListAsync(ct);

    public async Task<Result<bool>> RetryDeliveryAsync(long hookId, long deliveryId, CancellationToken ct = default)
    {
        var d = await db.WebhookDeliveries.FirstOrDefaultAsync(x => x.Id == deliveryId && x.IntegrationHookId == hookId, ct);
        if (d is null) return Result<bool>.Fail("سجل التسليم غير موجود");
        d.Status = DeliveryStatus.Pending;
        d.NextAttemptAt = DateTime.UtcNow;
        d.ErrorMessage = null;
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    private static IntegrationHookDto Map(IntegrationHook h) => new(
        h.Id, h.Name, h.EventType, h.CallbackUrl, h.HttpMethod, h.IsActive, h.MaxRetries,
        h.RetryDelaySeconds, h.InstitutionId, !string.IsNullOrWhiteSpace(h.SecretKey));
}
