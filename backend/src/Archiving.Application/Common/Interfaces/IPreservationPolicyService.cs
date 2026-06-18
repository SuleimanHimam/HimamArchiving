using Archiving.Application.Features.Preservation;

namespace Archiving.Application.Common.Interfaces;

/// <summary>The repository preservation policy (ISO 16363). Returns sensible defaults when none is set,
/// so behaviour is unchanged until an administrator configures it.</summary>
public interface IPreservationPolicyService
{
    Task<PreservationPolicyDto> GetAsync(CancellationToken ct = default);
    Task<PreservationPolicyDto> UpdateAsync(PreservationPolicyDto request, CancellationToken ct = default);
}
