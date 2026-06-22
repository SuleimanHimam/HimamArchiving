using Archiving.Application.Common.Models;
using Archiving.Application.Features.Disposition;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Two-step Retention &amp; Disposition workflow: Verification (Records Officer) → Final Approval
/// (Legal/Department Head), with segregation of duties and physical-slot release on destruction.</summary>
public interface IDispositionService
{
    /// <summary>List requests, optionally filtered to a queue stage: "Verification" | "FinalApproval".</summary>
    Task<PagedResult<DispositionRequestDto>> ListAsync(string? stage, int page, int pageSize, CancellationToken ct = default);
    Task<Result<DispositionRequestDto>> GetAsync(long id, CancellationToken ct = default);
    Task<Result<DispositionRequestDto>> CreateAsync(CreateDispositionRequest request, CancellationToken ct = default);
    Task<Result<DispositionRequestDto>> VerifyAsync(long id, VerifyDispositionRequest request, CancellationToken ct = default);
    Task<Result<DispositionRequestDto>> FinalApproveAsync(long id, FinalApproveDispositionRequest request, CancellationToken ct = default);
    Task<Result<DispositionRequestDto>> RejectAsync(long id, RejectDispositionRequest request, CancellationToken ct = default);
    Task<Result<DispositionCertificateDto>> GetCertificateAsync(long requestId, CancellationToken ct = default);
}
