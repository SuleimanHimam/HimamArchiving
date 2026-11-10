using System.Text.Json;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Domain.Events;
using Archiving.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.EventHandlers;

/// <summary>Open-generic handler that, for any domain event, enqueues a <see cref="WebhookDelivery"/> for every
/// active <see cref="IntegrationHook"/> subscribed to that event type. The delivery worker then POSTs them.</summary>
public sealed class WebhookDispatcherHandler<TEvent>(AppDbContext db)
    : INotificationHandler<TEvent> where TEvent : IDomainEvent
{
    public async Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        var eventType = typeof(TEvent).Name;
        var hooks = await db.IntegrationHooks
            .Where(h => h.IsActive && h.EventType == eventType)
            .Select(h => h.Id).ToListAsync(cancellationToken);
        if (hooks.Count == 0) return;

        var payload = JsonSerializer.Serialize(notification, notification.GetType());
        foreach (var hookId in hooks)
            db.WebhookDeliveries.Add(new WebhookDelivery
            {
                IntegrationHookId = hookId,
                EventType = eventType,
                PayloadJson = payload,
                Status = DeliveryStatus.Pending,
                NextAttemptAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync(cancellationToken);
    }
}
