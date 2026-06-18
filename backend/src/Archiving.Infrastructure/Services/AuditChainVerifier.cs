using Archiving.Domain.Entities;

namespace Archiving.Infrastructure.Services;

/// <summary>Result of verifying the audit hash chain (ISO 15489 / 16363 tamper-evidence).</summary>
public sealed record AuditChainResult(bool Intact, int Total, int Verified, long? FirstBrokenId, string? Detail);

/// <summary>Pure verification of the audit hash chain: re-links and re-hashes every entry.
/// Operates on an in-memory list so it is trivially unit-testable.</summary>
public static class AuditChainVerifier
{
    /// <param name="orderedById">Audit entries in ascending Id order.</param>
    public static AuditChainResult Verify(IReadOnlyList<AuditLog> orderedById)
    {
        string? previousHash = null;
        var verified = 0;

        foreach (var e in orderedById)
        {
            if (e.PreviousHash != previousHash)
                return new AuditChainResult(false, orderedById.Count, verified, e.Id,
                    "كسر في تسلسل السجل: قيمة التجزئة السابقة لا تطابق السجل الذي قبله");

            var expected = AuditHash.Compute(e, e.PreviousHash);
            if (!string.Equals(expected, e.Hash, StringComparison.Ordinal))
                return new AuditChainResult(false, orderedById.Count, verified, e.Id,
                    "تعديل على محتوى السجل: قيمة التجزئة لا تطابق المحتوى");

            verified++;
            previousHash = e.Hash;
        }

        return new AuditChainResult(true, orderedById.Count, verified, null, null);
    }
}
