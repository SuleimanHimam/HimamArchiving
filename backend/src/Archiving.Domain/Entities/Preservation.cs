using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

/// <summary>Outcome of a fixity (integrity) verification run.</summary>
public enum FixityResult
{
    Verified = 0,   // recomputed checksum matches the value stored at ingest
    Failed = 1,     // mismatch — the stored bytes changed (corruption/tampering)
    Missing = 2,    // the stored file could not be found
    NoBaseline = 3, // no checksum was recorded at ingest to compare against
}

/// <summary>An append-only record of a fixity check on a stored file (ISO 16363 — periodic
/// integrity verification + provenance). Never updated; one row per verification.</summary>
public class FixityCheck : BaseEntity
{
    public long DocumentAttachmentId { get; set; }
    public string Algorithm { get; set; } = "SHA-256";
    public string? ExpectedHash { get; set; }   // checksum recorded at ingest
    public string? ActualHash { get; set; }     // checksum recomputed now
    public FixityResult Result { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public long? CheckedByUserId { get; set; }  // null = automated/background sweep
    public string? Note { get; set; }

    public DocumentAttachment DocumentAttachment { get; set; } = null!;
}

/// <summary>Repository preservation policy (ISO 16363): the rules that govern long-term preservation —
/// target format, fixity algorithm/cadence, and whether ingest auto-normalizes. A single config record.</summary>
public class PreservationPolicy : BaseEntity
{
    public string Name { get; set; } = "سياسة الحفظ";
    public string? Description { get; set; }
    public string TargetPdfAConformance { get; set; } = "PDF/A-2B"; // preservation master format
    public bool AutoNormalizeOnIngest { get; set; } = true;          // generate a PDF/A master on scan ingest
    public string FixityAlgorithm { get; set; } = "SHA-256";
    public int FixityCadenceDays { get; set; } = 1;                  // how often the fixity sweep runs
    public string? AllowedPreservationFormats { get; set; } = "PDF/A, JPEG, PNG, TIFF"; // informational
}
