using Archiving.Application.Common.Models;
using Archiving.Application.Features.InternalMail;

namespace Archiving.Application.Common.Interfaces;

/// <summary>المراسلات — internal correspondence between staff/departments: addressing, per-user read
/// tracking, threaded replies (conversation), archiving, and search.</summary>
public interface IInternalMailService
{
    Task<Result<InternalDetailDto>> ComposeAsync(ComposeInternalRequest request, CancellationToken ct = default);
    Task<Result<InternalDetailDto>> ReplyAsync(long id, ReplyInternalRequest request, CancellationToken ct = default);
    Task<Result<InternalDetailDto>> ForwardAsync(long id, ForwardInternalRequest request, CancellationToken ct = default);

    Task<Result<InternalDetailDto>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<PagedResult<InternalListItemDto>> ListAsync(InternalListFilter filter, CancellationToken ct = default);
    Task<PagedResult<InternalListItemDto>> SearchAsync(string query, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<InternalThreadNodeDto>> GetThreadAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<InternalReadDto>> GetReadStatusAsync(long id, CancellationToken ct = default);

    Task<Result<bool>> MarkReadAsync(long id, CancellationToken ct = default);
    Task<Result<bool>> ArchiveAsync(long id, CancellationToken ct = default);
    Task<Result<bool>> SoftDeleteAsync(long id, CancellationToken ct = default);
    Task<Result<bool>> RestoreAsync(long id, CancellationToken ct = default);

    Task<Result<InternalAttachmentDto>> AddAttachmentAsync(long id, string fileName, string? contentType, byte[] content, CancellationToken ct = default);

    /// <summary>Opens an attachment's bytes for download/printing (same clearance check as the mail).</summary>
    Task<Result<AttachmentDownload>> DownloadAttachmentAsync(long id, long attachmentId, CancellationToken ct = default);
}
