using Archiving.Application.Common.Models;
using Archiving.Application.Features.Documents;

namespace Archiving.Application.Common.Interfaces;

/// <summary>An attachment stream plus the metadata needed to return it to the client.</summary>
public sealed record AttachmentDownload(Stream Content, string FileName, string ContentType);

public interface IDocumentService
{
    // Configuration
    Task<IReadOnlyList<DocumentTypeDto>> ListTypesAsync(long? orgUnitId = null, CancellationToken ct = default);
    Task<Result<DocumentTypeDto>> CreateTypeAsync(CreateDocumentTypeRequest request, CancellationToken ct = default);
    Task<Result<DocumentTypeDto>> UpdateTypeAsync(long id, UpdateDocumentTypeRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteTypeAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentCategoryDto>> ListCategoriesAsync(CancellationToken ct = default);
    Task<Result<DocumentCategoryDto>> CreateCategoryAsync(CreateDocumentCategoryRequest request, CancellationToken ct = default);
    Task<Result<DocumentCategoryDto>> UpdateCategoryAsync(long id, UpdateDocumentCategoryRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteCategoryAsync(long id, CancellationToken ct = default);

    // Documents
    Task<PagedResult<DocumentListItem>> ListAsync(DocumentQuery query, CancellationToken ct = default);
    Task<Result<DocumentDetail>> GetAsync(long id, CancellationToken ct = default);

    /// <summary>The document's verification code, issued on the spot if it has none yet.</summary>
    Task<Result<DocumentVerificationDto>> GetVerificationAsync(long id, CancellationToken ct = default);
    /// <summary>Read-only metadata card: who created and last changed it, and every attached
    /// file with its size and uploader.</summary>
    Task<Result<DocumentProperties>> GetPropertiesAsync(long id, CancellationToken ct = default);
    Task<Result<DocumentDetail>> CreateAsync(CreateDocumentRequest request, CancellationToken ct = default);
    Task<Result<DocumentDetail>> UpdateAsync(long id, UpdateDocumentRequest request, CancellationToken ct = default);
    Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default);

    // ---- Trash (soft-deleted documents) ----
    /// <summary>Everything the caller deleted and could still put back.</summary>
    Task<IReadOnlyList<TrashItem>> ListTrashAsync(CancellationToken ct = default);
    /// <summary>Undo a delete: the document returns to the register exactly as it was.</summary>
    Task<Result<bool>> RestoreAsync(long id, CancellationToken ct = default);
    /// <summary>Destroy one item's content for good — crypto-shred, then tombstone.</summary>
    Task<Result<bool>> PurgeAsync(long id, CancellationToken ct = default);
    /// <summary>The same, for everything in the trash. Returns how many were destroyed.</summary>
    Task<Result<int>> EmptyTrashAsync(CancellationToken ct = default);
    /// <summary>Submit a document to the Archive department: starts the archive-intake workflow.</summary>
    Task<Result<bool>> SendToArchiveAsync(long id, CancellationToken ct = default);

    // ---- The Archive department's side of that hand-over ----

    /// <summary>Everything waiting for the archive to give it a physical place, oldest first.</summary>
    Task<IReadOnlyList<ArchiveIntakeItem>> ListArchiveIntakeAsync(CancellationToken ct = default);

    /// <summary>Put a waiting document in a box: the document becomes مؤرشف and its submitter is told.</summary>
    Task<Result<bool>> FileToArchiveAsync(long id, FileToArchiveRequest request, CancellationToken ct = default);

    /// <summary>Hand a waiting document back to its submitter with a reason; it becomes active again
    /// and can be corrected and re-sent.</summary>
    Task<Result<bool>> ReturnFromArchiveAsync(long id, ReturnFromArchiveRequest request, CancellationToken ct = default);

    // Attachments. <paramref name="isScanned"/> marks the upload as coming from a scanner
    // (via the user's local scan agent) and is validated against the type's allowed upload sources.
    Task<Result<DocumentAttachmentDto>> AddAttachmentAsync(
        long documentId, string fileName, string contentType, Stream content,
        bool isScanned = false, CancellationToken ct = default);
    Task<Result<AttachmentDownload>> DownloadAttachmentAsync(long documentId, long attachmentId, CancellationToken ct = default);
    Task<Result<bool>> RemoveAttachmentAsync(long documentId, long attachmentId, CancellationToken ct = default);
}
