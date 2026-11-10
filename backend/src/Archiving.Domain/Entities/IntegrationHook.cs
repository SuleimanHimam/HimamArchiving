using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>An external HTTP subscription to a domain event type (Phase 11).</summary>
public class IntegrationHook : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;     // matches a domain event class name (e.g. DocumentCreatedEvent)
    public string CallbackUrl { get; set; } = string.Empty;
    public string? HttpMethod { get; set; } = "POST";
    public string? HeadersJson { get; set; }                  // JSON object of custom headers (e.g. API keys)
    public string? SecretKey { get; set; }                    // HMAC-SHA256 signing secret
    public bool IsActive { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 60;
    public long? InstitutionId { get; set; }
}

/// <summary>One delivery attempt of an event payload to an <see cref="IntegrationHook"/> (Phase 11).</summary>
public class WebhookDelivery : BaseEntity
{
    public long IntegrationHookId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public int AttemptNumber { get; set; }
    public int? HttpStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public string? ErrorMessage { get; set; }
}
