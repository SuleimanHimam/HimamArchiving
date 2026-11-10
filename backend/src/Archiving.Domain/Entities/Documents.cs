using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>Optional classification tree for organizing document types/documents.</summary>
public class DocumentCategory : BaseEntity
{
    public long? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsActive { get; set; } = true;

    public DocumentCategory? Parent { get; set; }
    public ICollection<DocumentCategory> Children { get; set; } = new List<DocumentCategory>();
}

/// <summary>Configurable document type carrying its own settings (classification, retention, workflow, upload rules).</summary>
public class DocumentType : BaseEntity
{
    public string Name { get; set; } = string.Empty;          // e.g. Contract, Invoice, Memo, Report
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public long? CategoryId { get; set; }

    // Ownership: a type may belong to one department (الدائرة) or be shared. null = عام,
    // offered to every department; set = only that department's catalogue (نوع المرفق).
    public long? OrgUnitId { get; set; }
    public int? Number { get; set; }                          // الرقم — the department's own reference no.
    public string? Note { get; set; }                         // ملاحظة

    public ConfidentialityLevel DefaultConfidentiality { get; set; } = ConfidentialityLevel.Internal;
    // Retention is entered as a value + unit (days / months / years) and kept exactly, so a
    // period in days is not lost to rounding. RetentionMonths stays as the derived value the
    // lifecycle/disposition code already reads — always recomputed from the pair on save.
    public int RetentionValue { get; set; } = 10;
    public RetentionUnit RetentionUnit { get; set; } = RetentionUnit.Years;
    public int RetentionMonths { get; set; } = 120;           // derived; default 10 years
    public long? DefaultWorkflowDefinitionId { get; set; }    // dedicated workflow for this type
    public bool RequiresApproval { get; set; }
    public bool RequiresPhysicalArchiving { get; set; }       // can be sent to the Archive dept for physical filing
    public UploadSource AllowedUploadSources { get; set; } = UploadSource.All; // scanner-only restriction supported
    public bool IsActive { get; set; } = true;

    public DocumentCategory? Category { get; set; }
    public OrgUnit? OrgUnit { get; set; }
}

/// <summary>Core document record + metadata. Owned by an org unit and a position (position-based access).</summary>
public class Document : SoftDeleteEntity
{
    public string DocumentNumber { get; set; } = string.Empty; // auto-generated, unique
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long DocumentTypeId { get; set; }
    public long? CategoryId { get; set; }
    public long OwningOrgUnitId { get; set; }                  // owning entity
    public long? OwnerPositionId { get; set; }                 // responsible seat
    public ConfidentialityLevel Confidentiality { get; set; } = ConfidentialityLevel.Internal;
    public DocumentStatus Status { get; set; } = DocumentStatus.Active;
    public string? Keywords { get; set; }                      // space/comma separated; covered by full-text index
    public int RetentionMonths { get; set; }
    public DateOnly? DocumentDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }                  // computed from retention; drives lifecycle alerts

    /// <summary>
    /// The code printed on the paper as a QR, so a recipient can ask this system whether it issued
    /// the document in their hand. Assigned at registration; null on rows written before this
    /// existed, and filled in the first time such a document is opened — see
    /// <see cref="DocumentVerification"/> for why it is a random code rather than the id.
    /// </summary>
    public string? VerificationCode { get; set; }

    // Versioning
    public int Version { get; set; } = 1;
    public long? ParentDocumentId { get; set; }
    public bool IsLatestVersion { get; set; } = true;

    public long? FolderId { get; set; }                        // optional personal folder of the creator
    public long? BoxId { get; set; }                           // exact physical box (normalized location hierarchy)

    // Disposition / destruction (tombstone): content destroyed but the record's metadata is retained as proof.
    public bool IsTombstone { get; set; }
    public DateTime? DestroyedAtUtc { get; set; }
    public long? DestructionCertificateId { get; set; }

    public long? ArchiveWorkflowInstanceId { get; set; }      // the in-flight archive-intake workflow, if any
    public string? ArchiveReturnReason { get; set; }          // set when the Archive dept returns it to the uploader
    // When it was handed to the archive. The intake queue is worked oldest-first, and "how long has
    // this been waiting" is not answerable from CreatedAt (registration) or UpdatedAt (any edit).
    public DateTime? SentToArchiveAtUtc { get; set; }

    // Archiving path. Correspondence-linked archiving was removed; documents are archived manually.
    public ArchiveMode ArchiveMode { get; set; } = ArchiveMode.Manual;

    public DocumentType DocumentType { get; set; } = null!;
    public DocumentCategory? Category { get; set; }
    public OrgUnit OwningOrgUnit { get; set; } = null!;
    public Position? OwnerPosition { get; set; }
    public Document? ParentDocument { get; set; }
    public ICollection<DocumentAttachment> Attachments { get; set; } = new List<DocumentAttachment>();

    /// <summary>Moves the document to <paramref name="newStatus"/> after validating the transition against
    /// <see cref="StateMachine.DocumentLifecycle"/>. Raises <see cref="Events.DocumentStatusChangedEvent"/>.
    /// Throws <see cref="Common.DomainException"/> on an illegal transition. (Permission is enforced by the caller.)</summary>
    public void TransitionTo(DocumentStatus newStatus, string? triggeredByUserId = null)
    {
        if (Status == newStatus) return;
        if (!StateMachine.DocumentLifecycle.CanTransition(Status, newStatus))
            throw new Common.DomainException($"انتقال غير مسموح لحالة الوثيقة: {Status} → {newStatus}");

        var old = Status;
        Status = newStatus;
        AddDomainEvent(new Events.DocumentStatusChangedEvent(Id, old.ToString(), newStatus.ToString())
        {
            TriggeredByUserId = triggeredByUserId,
        });
    }
}

public class DocumentAttachment : BaseEntity
{
    public long DocumentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;   // MIME
    public string FileExtension { get; set; } = string.Empty;  // pdf|docx|xlsx|jpg|png|zip
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = string.Empty;     // object-storage key / disk path
    public string? Checksum { get; set; }                      // fixity value generated at ingest
    public string ChecksumAlgorithm { get; set; } = "SHA-256"; // ISO 16363 — algorithm used for the fixity value
    public DateTime? LastFixityCheckAt { get; set; }           // last time the stored file was re-verified
    public bool IsScanned { get; set; }

    // Encryption at rest (optional): when set, the stored bytes are AES-encrypted with this IV.
    public bool IsEncrypted { get; set; }
    public string? EncryptionIv { get; set; }

    // Disposition: set when the content has been destroyed (crypto-shred / secure-overwrite). The row and
    // its checksum survive as proof-of-prior-existence; the bytes are gone.
    public bool ContentDestroyed { get; set; }
    public DateTime? ContentDestroyedAt { get; set; }

    // Full-text search (extracted from the file: embedded PDF text or OCR for scans)
    public string? ExtractedText { get; set; }                 // searchable text content
    public TextExtractionStatus ExtractionStatus { get; set; } = TextExtractionStatus.Pending;
    public string? ExtractionSource { get; set; }              // "Embedded" | "OCR"
    public DateTime? TextExtractedAt { get; set; }

    // OAIS / PDF-A preservation (ISO 14721, 19005)
    public AttachmentKind Kind { get; set; } = AttachmentKind.Original; // submitted original vs preservation master
    public long? SourceAttachmentId { get; set; }              // for a preservation master: the original it derives from
    public string? PdfAConformance { get; set; }               // e.g. "PDF/A-2B" (preservation masters only)
    public bool PreservationValidated { get; set; }            // veraPDF confirmed conformance
    public string? PreservationNote { get; set; }              // validator output / why a copy wasn't made

    public Document Document { get; set; } = null!;
}

/// <summary>OAIS role of a stored file: the submitted original (SIP) or the long-term
/// preservation master (AIP).</summary>
public enum AttachmentKind
{
    Original = 0,
    PreservationMaster = 1,
}
