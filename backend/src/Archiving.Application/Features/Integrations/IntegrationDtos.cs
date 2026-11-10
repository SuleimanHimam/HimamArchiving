namespace Archiving.Application.Features.Integrations;

public sealed record IntegrationHookDto(
    long Id, string Name, string EventType, string CallbackUrl, string? HttpMethod,
    bool IsActive, int MaxRetries, int RetryDelaySeconds, long? InstitutionId, bool HasSecret);

public sealed record CreateIntegrationHookDto(
    string Name, string EventType, string CallbackUrl, string? HttpMethod,
    string? HeadersJson, string? SecretKey, int MaxRetries = 3, int RetryDelaySeconds = 60, long? InstitutionId = null);

public sealed record WebhookDeliveryDto(
    long Id, long IntegrationHookId, string EventType, int AttemptNumber, int? HttpStatusCode,
    string Status, DateTime? DeliveredAt, DateTime? NextAttemptAt, string? ErrorMessage, DateTime CreatedAt);
