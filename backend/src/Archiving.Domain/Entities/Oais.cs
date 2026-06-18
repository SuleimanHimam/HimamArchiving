using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

/// <summary>OAIS information-package kind (ISO 14721).</summary>
public enum PackageType
{
    Submission = 0,    // SIP — what was submitted at ingest
    Archival = 1,      // AIP — what is preserved long-term
    Dissemination = 2, // DIP — what is handed out on access
}

/// <summary>An OAIS Information Package wrapping a document's files + a manifest snapshot
/// (file list, checksums, metadata). Persisted per document and refreshed on demand.</summary>
public class InformationPackage : BaseEntity
{
    public long DocumentId { get; set; }
    public PackageType Type { get; set; }
    public int FileCount { get; set; }
    public long TotalBytes { get; set; }
    public string Manifest { get; set; } = "{}";   // JSON snapshot (files, checksums, metadata)

    public Document Document { get; set; } = null!;
}

/// <summary>Representation Information for a stored file (ISO 14721): the format identity needed to
/// render it in the future — MIME type, PRONOM format id, and a human note.</summary>
public class RepresentationInfo : BaseEntity
{
    public long DocumentAttachmentId { get; set; }
    public string? FormatName { get; set; }     // e.g. "PDF/A-2b", "JPEG"
    public string? MimeType { get; set; }
    public string? PronomPuid { get; set; }     // e.g. "fmt/477"
    public string? RenderingNote { get; set; }

    public DocumentAttachment DocumentAttachment { get; set; } = null!;
}

/// <summary>The community the archive serves and the rendering it expects (ISO 14721 Designated
/// Community). A single configurable record.</summary>
public class DesignatedCommunity : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RenderingExpectations { get; set; } // e.g. "PDF/A readers; Arabic (utf8mb4)"
}
