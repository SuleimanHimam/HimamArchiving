using Archiving.Application.Common.Models;
using Archiving.Application.Features.Diwan;

namespace Archiving.Application.Common.Interfaces;

/// <summary>الديوان — the external correspondence register (incoming/outgoing letters to external bodies).</summary>
public interface IDiwanService
{
    Task<Result<DiwanDetailDto>> CreateAsync(SaveDiwanRequest request, CancellationToken ct = default);
    Task<Result<DiwanDetailDto>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<PagedResult<DiwanListItemDto>> ListAsync(DiwanListFilter filter, CancellationToken ct = default);
    Task<PagedResult<DiwanListItemDto>> SearchAsync(string query, int page, int pageSize, CancellationToken ct = default);

    /// <summary>File a reply to a register entry, in the opposite direction and linked as its مرجع.</summary>
    Task<Result<DiwanDetailDto>> ReplyAsync(long id, ReplyDiwanRequest request, CancellationToken ct = default);

    /// <summary>The full chain for a register entry: the original, its replies, and entries citing it as مرجع.</summary>
    Task<IReadOnlyList<DiwanThreadNodeDto>> GetThreadAsync(long id, CancellationToken ct = default);

    /// <summary>Convert a register entry into an internal message (مراسلة داخلية). Returns the new
    /// internal message id; the register entry itself is left in place.</summary>
    Task<Result<long>> ConvertToInternalAsync(long id, ConvertToInternalRequest request, CancellationToken ct = default);

    /// <summary>من يطّلع على البريد — who can open this letter, and whether the caller may change it.</summary>
    Task<Result<DiwanAccessViewDto>> GetAccessAsync(long id, CancellationToken ct = default);

    /// <summary>Names the staff let in on one external letter. Reserved to the administrator and to
    /// the head of a department the letter reached — routing addresses a letter to a department,
    /// and the head says who in it handles the letter.</summary>
    Task<Result<bool>> SetAccessAsync(long id, SetDiwanAccessRequest request, CancellationToken ct = default);

    /// <summary>حالة البريد — set the register entry's status, recording the old/new values in the audit trail.</summary>
    Task<Result<bool>> ChangeStatusAsync(long id, ChangeDiwanStatusRequest request, CancellationToken ct = default);

    Task<Result<bool>> ArchiveAsync(long id, CancellationToken ct = default);
    Task<Result<bool>> SoftDeleteAsync(long id, CancellationToken ct = default);
    Task<Result<bool>> RestoreAsync(long id, CancellationToken ct = default);

    Task<Result<DiwanAttachmentDto>> AddAttachmentAsync(long id, string fileName, string? contentType, byte[] content, CancellationToken ct = default);

    /// <summary>Opens an attachment's bytes for download/printing (same clearance check as the mail).</summary>
    Task<Result<AttachmentDownload>> DownloadAttachmentAsync(long id, long attachmentId, CancellationToken ct = default);

    /// <summary>تصنيف البريد lookup (seeded with a default set on first use). Pass
    /// <paramref name="scope"/> (0 External · 1 Internal) to get only the types offered on that
    /// register — types marked Both appear in either. Null returns the whole catalogue, which is
    /// what the settings screen and the print designer list.</summary>
    Task<IReadOnlyList<MailCategoryDto>> ListCategoriesAsync(int? scope = null, bool includeInactive = false, CancellationToken ct = default);

    Task<Result<MailCategoryDto>> CreateCategoryAsync(SaveMailCategoryRequest request, CancellationToken ct = default);
    Task<Result<MailCategoryDto>> UpdateCategoryAsync(long id, SaveMailCategoryRequest request, CancellationToken ct = default);

    /// <summary>Removes a mail type. Refuses while any letter still carries it — deactivate instead.</summary>
    Task<Result<bool>> DeleteCategoryAsync(long id, CancellationToken ct = default);
}
