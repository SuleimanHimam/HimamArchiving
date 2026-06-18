using Archiving.Application.Common.Models;
using Archiving.Application.Features.Notifications;

namespace Archiving.Application.Common.Interfaces;

public interface INotificationService
{
    Task<NotificationListResult> ListMineAsync(bool unreadOnly, CancellationToken ct = default);
    Task<int> UnreadCountAsync(CancellationToken ct = default);
    Task<Result<bool>> MarkReadAsync(long id, CancellationToken ct = default);
    Task<Result<int>> MarkAllReadAsync(CancellationToken ct = default);
}
