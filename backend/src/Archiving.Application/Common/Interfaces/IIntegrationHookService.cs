using Archiving.Application.Common.Models;
using Archiving.Application.Features.Integrations;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Manages outbound webhook subscriptions and their delivery history (Phase 11).</summary>
public interface IIntegrationHookService
{
    Task<IReadOnlyList<IntegrationHookDto>> ListAsync(CancellationToken ct = default);
    Task<Result<IntegrationHookDto>> CreateAsync(CreateIntegrationHookDto dto, CancellationToken ct = default);
    Task<Result<IntegrationHookDto>> UpdateAsync(long id, CreateIntegrationHookDto dto, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default);
    /// <summary>Enqueues a test payload for immediate delivery.</summary>
    Task<Result<bool>> TestAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookDeliveryDto>> GetDeliveriesAsync(long hookId, CancellationToken ct = default);
    Task<Result<bool>> RetryDeliveryAsync(long hookId, long deliveryId, CancellationToken ct = default);
}
