using Archiving.Application.Common.Models;
using Archiving.Application.Features.Preservation;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Generates PDF/A preservation masters from submitted files (ISO 19005 / 14721).</summary>
public interface IPreservationService
{
    /// <summary>Creates a PDF/A-2b preservation master from an attachment (image sources). Idempotent:
    /// returns the existing master if one was already generated.</summary>
    Task<Result<PreservationCopyDto>> GeneratePreservationCopyAsync(long attachmentId, CancellationToken ct = default);
}
