using Archiving.Application.Common.Models;
using Archiving.Application.Features.Escalation;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Admin CRUD for escalation rules plus read access to the escalation history.</summary>
public interface IEscalationService
{
    Task<IReadOnlyList<EscalationRuleDto>> ListRulesAsync(CancellationToken ct = default);
    Task<Result<EscalationRuleDto>> CreateRuleAsync(CreateEscalationRuleDto dto, CancellationToken ct = default);
    Task<Result<EscalationRuleDto>> UpdateRuleAsync(long id, CreateEscalationRuleDto dto, CancellationToken ct = default);
    Task<Result<bool>> DeleteRuleAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<EscalationLogDto>> GetLogAsync(long? workflowDefinitionId, DateTime? from, DateTime? to, CancellationToken ct = default);
}
