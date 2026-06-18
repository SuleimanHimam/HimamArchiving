using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>In-app notification record. Email/SMS/Push delivery is performed by services and tracked per channel.</summary>
public class Notification : BaseEntity
{
    public long RecipientUserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public NotificationType Type { get; set; } = NotificationType.Info;
    public NotificationChannel Channels { get; set; } = NotificationChannel.InApp;
    public string? EntityType { get; set; }              // deep-link target
    public long? EntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsEscalation { get; set; }

    public User Recipient { get; set; } = null!;
}

/// <summary>Per-channel delivery attempt for a notification (email/SMS/push), for retry &amp; reporting.</summary>
public class NotificationDelivery : BaseEntity
{
    public long NotificationId { get; set; }
    public NotificationChannel Channel { get; set; }
    public bool Succeeded { get; set; }
    public DateTime? SentAt { get; set; }
    public string? Error { get; set; }

    public Notification Notification { get; set; } = null!;
}

/// <summary>Append-only, tamper-evident audit log. Each row chains to the previous via a hash
/// (PreviousHash + content) so any modification breaks the chain.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public string Action { get; set; } = string.Empty;   // Login | Create | Edit | Delete | Print | Forward | Approve ...
    public string? EntityType { get; set; }
    public long? EntityId { get; set; }
    public string? EntityTitle { get; set; }
    public string? OldValues { get; set; }               // JSON
    public string? NewValues { get; set; }               // JSON
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? MachineName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Tamper-evidence (hash chain)
    public string? PreviousHash { get; set; }
    public string Hash { get; set; } = string.Empty;
}
