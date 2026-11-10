using System.Text.Json;
using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Domain.Events;
using Archiving.Infrastructure.Persistence;
using MediatR;

namespace Archiving.Infrastructure.EventHandlers;

/// <summary>
/// Open-generic handler that serializes <em>every</em> domain event into the EventLog table,
/// giving a complete, queryable event audit trail. Registered once as an open generic in DI.
/// </summary>
public sealed class EventLogHandler<TEvent>(AppDbContext db, ICurrentUser currentUser)
    : INotificationHandler<TEvent> where TEvent : IDomainEvent
{
    public async Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        db.EventLogs.Add(new EventLog
        {
            EventType = typeof(TEvent).Name,
            EventData = JsonSerializer.Serialize(notification, notification.GetType()),
            TriggeredByUserId = notification.TriggeredByUserId ?? currentUser.UserId?.ToString(),
            OccurredAt = notification.OccurredAt,
            ProcessedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
