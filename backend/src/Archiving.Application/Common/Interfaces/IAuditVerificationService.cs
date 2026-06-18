using Archiving.Application.Features.Preservation;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Verifies the integrity of the tamper-evident audit hash chain — ISO 15489 / 16363.</summary>
public interface IAuditVerificationService
{
    Task<AuditChainReport> VerifyChainAsync(CancellationToken ct = default);

    /// <summary>One-time baseline operation: recomputes every entry's chained hash with the current
    /// algorithm. Used only to adopt a corrected hashing scheme; admin-gated and audited.</summary>
    Task<int> ResealAsync(CancellationToken ct = default);
}
