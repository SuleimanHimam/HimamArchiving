namespace Archiving.Application.Features.Notifications;

public sealed record NotificationDto(
    long Id,
    string Title,
    string? Body,
    string Type,
    string? EntityType,
    long? EntityId,
    bool IsRead,
    bool IsEscalation,
    DateTime CreatedAt);

public sealed record NotificationListResult(
    IReadOnlyList<NotificationDto> Items,
    int UnreadCount);
