using Archiving.Application.Common.Models;
using Archiving.Application.Features.Preservation;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Fixity (integrity) verification of stored files — ISO 16363.</summary>
public interface IFixityService
{
    /// <summary>Re-reads a stored file, recomputes its checksum and compares to the value
    /// recorded at ingest; logs the outcome.</summary>
    Task<Result<FixityCheckDto>> VerifyAttachmentAsync(long attachmentId, CancellationToken ct = default);

    /// <summary>Most recent fixity checks (newest first) for reporting.</summary>
    Task<IReadOnlyList<FixityCheckDto>> RecentChecksAsync(int take = 100, CancellationToken ct = default);

    /// <summary>Verifies up to <paramref name="max"/> least-recently-checked attachments
    /// (used by the background sweep). Returns the number that failed.</summary>
    Task<int> SweepAsync(int max, CancellationToken ct = default);
}
