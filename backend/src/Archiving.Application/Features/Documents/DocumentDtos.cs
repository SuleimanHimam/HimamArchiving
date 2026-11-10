using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Documents;

// ---- Document types & categories (configuration) ----

public sealed record DocumentTypeDto(
    long Id,
    string Name,
    string? NameEn,
    string? Code,
    long? CategoryId,
    string DefaultConfidentiality,
    int RetentionMonths,           // derived from RetentionValue/RetentionUnit
    bool RequiresApproval,
    string AllowedUploadSources,   // flags, e.g. "Scanner" (scanner-only) or "All"
    bool IsActive,
    int RetentionValue,            // مدة الحفظ as entered
    string RetentionUnit,          // "Days" | "Months" | "Years"
    long? OrgUnitId = null,        // owning الدائرة; null = عام (offered to every department)
    string? OrgUnitName = null,
    int? Number = null,            // الرقم — the department's own reference number
    string? Note = null);          // ملاحظة

public sealed record CreateDocumentTypeRequest(
    string Name,
    string? NameEn,
    string? Code,
    long? CategoryId,
    ConfidentialityLevel DefaultConfidentiality,
    int RetentionMonths,
    bool RequiresApproval,
    UploadSource AllowedUploadSources = UploadSource.All,
    bool IsActive = true,
    long? OrgUnitId = null,        // null = عام
    int? Number = null,
    string? Note = null,
    int RetentionValue = 10,       // RetentionMonths is derived from this + the unit
    RetentionUnit RetentionUnit = RetentionUnit.Years);

public sealed record UpdateDocumentTypeRequest(
    string Name,
    string? NameEn,
    string? Code,
    long? CategoryId,
    ConfidentialityLevel DefaultConfidentiality,
    int RetentionMonths,
    bool RequiresApproval,
    UploadSource AllowedUploadSources,
    bool IsActive,
    long? OrgUnitId = null,
    int? Number = null,
    string? Note = null,
    int RetentionValue = 10,
    RetentionUnit RetentionUnit = RetentionUnit.Years);

public sealed record DocumentCategoryDto(long Id, long? ParentId, string Name, string? Code, bool IsActive);

public sealed record CreateDocumentCategoryRequest(long? ParentId, string Name, string? Code);

public sealed record UpdateDocumentCategoryRequest(long? ParentId, string Name, string? Code, bool IsActive);

// ---- Documents ----

public sealed record DocumentListItem(
    long Id,
    string DocumentNumber,
    string Title,
    string DocumentTypeName,
    string Confidentiality,
    string Status,
    int Version,
    DateOnly? DocumentDate,
    DateOnly? ExpiryDate,
    DateTime CreatedAt,
    // Physical storage place (latest linked location)
    string? PhysicalLocationName = null,
    string? BoxNumber = null,
    string? FileNumber = null,
    string? BoxCode = null,
    // Primary (first-uploaded) attachment's extension, so the register can show a
    // file-type card per row without a detail fetch. Null when nothing is attached.
    string? FileExtension = null,
    int AttachmentCount = 0,
    // Combined size of the attached files, and the last edit time — the file
    // browser's list view shows both as columns and sorts on them.
    long TotalSizeBytes = 0,
    DateTime? UpdatedAt = null,
    // Id of that same primary attachment, so a browser showing image documents can
    // fetch the real picture as a thumbnail instead of drawing a generic card.
    long? PrimaryAttachmentId = null)
{
    /// <summary>Admin-defined custom field values for this document, keyed by field id.</summary>
    public Dictionary<long, string> CustomValues { get; init; } = new();
}

public sealed record DocumentAttachmentDto(
    long Id,
    string FileName,
    string ContentType,
    string FileExtension,
    long SizeBytes,
    bool IsScanned,
    DateTime CreatedAt,
    string Kind = "Original",            // Original | PreservationMaster
    long? SourceAttachmentId = null,
    string? PdfAConformance = null,
    bool PreservationValidated = false);

/// <summary>One attached file as the properties panel shows it: what it is, how big, and
/// who put it there. Separate from <see cref="DocumentAttachmentDto"/> because that one is the
/// working payload for the viewer and gains preservation fields; this one is a read-only card.</summary>
public sealed record DocumentAttachmentInfo(
    long Id,
    string FileName,
    string FileExtension,
    long SizeBytes,
    bool IsScanned,
    DateTime CreatedAt,
    string? UploadedByName);

/// <summary>Everything the "properties" dialog shows for one document. Built by its own query
/// rather than folded into <see cref="DocumentDetail"/>: it needs user names, which the detail
/// mapper cannot reach, and the detail payload is already on every document page load.</summary>
public sealed record DocumentProperties(
    long Id,
    string DocumentNumber,
    string Title,
    string DocumentTypeName,
    string Confidentiality,
    string Status,
    int Version,
    DateOnly? DocumentDate,
    DateTime CreatedAt,
    string? CreatedByName,
    DateTime? UpdatedAt,
    string? UpdatedByName,
    string? FolderName,
    /// <summary>The folder's palette key and symbol. A folder is recognised by its colour in the
    /// browser; naming it here in plain text makes the reader match the two up by reading.</summary>
    string? FolderColor,
    string? FolderIcon,
    string? PhysicalLocationName,
    int AttachmentCount,
    long TotalSizeBytes,
    IReadOnlyList<DocumentAttachmentInfo> Attachments);

/// <summary>A soft-deleted document as the trash view lists it. Deliberately thin: the trash is
/// a place to recognise something and put it back, not to work in.</summary>
public sealed record TrashItem(
    long Id,
    string DocumentNumber,
    string Title,
    string DocumentTypeName,
    string? FileExtension,
    int AttachmentCount,
    long TotalSizeBytes,
    DateTime? DeletedAt,
    string? DeletedByName);

public sealed record DocumentDetail(
    long Id,
    string DocumentNumber,
    string Title,
    string? Description,
    long DocumentTypeId,
    string DocumentTypeName,
    long? CategoryId,
    long OwningOrgUnitId,
    long? OwnerPositionId,
    string Confidentiality,
    string Status,
    string? Keywords,
    int RetentionMonths,
    DateOnly? DocumentDate,
    DateOnly? ExpiryDate,
    int Version,
    long? ParentDocumentId,
    bool IsLatestVersion,
    DateTime CreatedAt,
    IReadOnlyList<DocumentAttachmentDto> Attachments,
    // Physical archive location (where the paper original is stored)
    long? PhysicalLocationId = null,
    string? PhysicalLocationName = null,
    string? BoxNumber = null,
    string? FileNumber = null,
    long? FolderId = null,
    bool IsFavorite = false,
    bool IsTombstone = false,
    DateTime? DestroyedAtUtc = null,
    long? BoxId = null,
    string? BoxCode = null,
    /// <summary>Why the archive department sent this back, if it did. The submitter has to be able
    /// to read the objection on the document itself, not only in the notification that announced it.</summary>
    string? ArchiveReturnReason = null,
    DateTime? SentToArchiveAtUtc = null);

public sealed record CreateDocumentRequest(
    string Title,
    string? Description,
    long DocumentTypeId,
    long? CategoryId,
    long OwningOrgUnitId,
    long? OwnerPositionId,
    ConfidentialityLevel Confidentiality,
    string? Keywords,
    DateOnly? DocumentDate,
    // Optional: link the physical storage location at entry time
    long? PhysicalLocationId = null,
    string? BoxNumber = null,
    string? FileNumber = null,
    long? BoxId = null,    // exact box in the normalized location hierarchy
    // How the document entered the archive: Manual (created directly) or via a mail register.
    ArchiveMode ArchiveMode = ArchiveMode.Manual);

public sealed record UpdateDocumentRequest(
    string Title,
    string? Description,
    long? CategoryId,
    long? OwnerPositionId,
    ConfidentialityLevel Confidentiality,
    string? Keywords,
    DateOnly? DocumentDate,
    DateOnly? ExpiryDate = null,    // admin override; when null, recomputed from retention
    long? BoxId = null);

/// <summary>A document sitting in the Archive department's intake queue, waiting to be given a
/// shelf. Carries who sent it and when, because the queue is worked oldest-first and the archivist
/// needs to know whose desk to go back to if something is wrong with it.</summary>
public sealed record ArchiveIntakeItem(
    long Id,
    string DocumentNumber,
    string Title,
    string DocumentTypeName,
    long OwningOrgUnitId,
    string OwningOrgUnitName,
    string Confidentiality,
    DateOnly? DocumentDate,
    DateTime? SentAt,
    string? SentByName,
    int AttachmentCount,
    /// <summary>Set when this document has been returned before — the archivist is looking at a
    /// second attempt, and the previous objection is the first thing worth seeing.</summary>
    string? PreviousReturnReason);

/// <summary>File an intake document into a physical box, completing its archiving.</summary>
public sealed record FileToArchiveRequest(long BoxId, string? Note);

/// <summary>Send an intake document back to whoever submitted it. The reason is mandatory: a
/// document that reappears in someone's list with no explanation cannot be acted on.</summary>
public sealed record ReturnFromArchiveRequest(string Reason);

public sealed record DocumentQuery(
    string? Search,
    DocumentStatus? Status,
    long? DocumentTypeId,
    long? OwningOrgUnitId,
    DateOnly? DateFrom = null,
    DateOnly? DateTo = null,
    bool FavoritesOnly = false,
    bool SharedWithMe = false,
    long? FolderId = null,
    long? CustomFieldId = null,
    string? CustomFieldValue = null,
    long? BoxId = null,            // documents filed into one physical box
    /// <summary>Filter by a place at any level of the physical hierarchy — Building, Room,
    /// Cabinet, Shelf or Box — paired with <see cref="LocationId"/>. Everything under that
    /// place is included: asking for a building returns the documents in every box inside it.
    /// <see cref="BoxId"/> stays as the narrow "this exact box" filter it always was.</summary>
    string? LocationKind = null,
    long? LocationId = null,
    /// <summary>Only documents that sit in no folder at all. The file browser's root
    /// needs this: without it a document filed into a folder still lists at the root,
    /// so it appears to be in two places at once.</summary>
    bool UnfiledOnly = false,
    int Page = 1,
    int PageSize = 20);

/// <summary>What the document page needs to print a verification QR: the code, and the two URLs
/// built from it — the page a scanner lands on, and the image to print.</summary>
public sealed record DocumentVerificationDto(long DocumentId, string DocumentNumber, string Code,
    string VerifyPath, string QrPath);
