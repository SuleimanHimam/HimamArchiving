using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Notifications;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class NotificationService(AppDbContext db, ICurrentUser currentUser) : INotificationService
{
    public async Task<NotificationListResult> ListMineAsync(bool unreadOnly, CancellationToken ct = default)
    {
        var me = currentUser.UserId;
        if (me is null) return new NotificationListResult([], 0);

        var q = db.Notifications.Where(n => n.RecipientUserId == me);
        if (unreadOnly) q = q.Where(n => !n.IsRead);

        var items = await q
            .OrderByDescending(n => n.Id)
            .Take(100)
            .Select(n => new NotificationDto(
                n.Id, n.Title, n.Body, n.Type.ToString(), n.EntityType, n.EntityId,
                n.IsRead, n.IsEscalation, n.CreatedAt))
            .ToListAsync(ct);

        var unread = await db.Notifications.CountAsync(n => n.RecipientUserId == me && !n.IsRead, ct);
        return new NotificationListResult(items, unread);
    }

    public async Task<int> UnreadCountAsync(CancellationToken ct = default)
    {
        var me = currentUser.UserId;
        if (me is null) return 0;
        return await db.Notifications.CountAsync(n => n.RecipientUserId == me && !n.IsRead, ct);
    }

    public async Task<Result<bool>> MarkReadAsync(long id, CancellationToken ct = default)
    {
        var me = currentUser.UserId;
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientUserId == me, ct);
        if (n is null) return Result<bool>.Fail("الإشعار غير موجود");
        if (!n.IsRead)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return Result<bool>.Ok(true);
    }

    public async Task<Result<int>> MarkAllReadAsync(CancellationToken ct = default)
    {
        var me = currentUser.UserId;
        if (me is null) return Result<int>.Ok(0);

        var now = DateTime.UtcNow;
        var updated = await db.Notifications
            .Where(n => n.RecipientUserId == me && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true).SetProperty(n => n.ReadAt, now), ct);
        return Result<int>.Ok(updated);
    }
}
