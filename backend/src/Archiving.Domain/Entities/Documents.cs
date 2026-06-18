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
    public ConfidentialityLevel DefaultConfidentiality { get; set; } = ConfidentialityLevel.Internal;
    public int RetentionMonths { get; set; } = 120;           // default 10 years
    public long? DefaultWorkflowDefinitionId { get; set; }    // dedicated workflow for this type
    public bool RequiresApproval { get; set; }
    public UploadSource AllowedUploadSources { get; set; } = UploadSource.All; // scanner-only restriction supported
    public bool IsActive { get; set; } = true;

    public DocumentCategory? Category { get; set; }
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

    // Versioning
    public int Version { get; set; } = 1;
    public long? ParentDocumentId { get; set; }
    public bool IsLatestVersion { get; set; } = true;

    public DocumentType DocumentType { get; set; } = null!;
    public DocumentCategory? Category { get; set; }
    public OrgUnit OwningOrgUnit { get; set; } = null!;
    public Position? OwnerPosition { get; set; }
    public Document? ParentDocument { get; set; }
    public ICollection<DocumentAttachment> Attachments { get; set; } = new List<DocumentAttachment>();
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
    public string? OcrText { get; set; }                       // populated in Phase 2 (OCR)

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
