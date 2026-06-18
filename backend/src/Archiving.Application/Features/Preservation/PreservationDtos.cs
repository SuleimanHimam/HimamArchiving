namespace Archiving.Application.Features.Preservation;

/// <summary>A single fixity (integrity) verification result.</summary>
public sealed record FixityCheckDto(
    long Id,
    long DocumentAttachmentId,
    long DocumentId,
    string FileName,
    string Algorithm,
    string Result,
    DateTime CheckedAt,
    long? CheckedByUserId,
    string? Note);

/// <summary>Result of verifying the tamper-evident audit hash chain.</summary>
public sealed record AuditChainReport(
    bool Intact,
    int Total,
    int Verified,
    long? FirstBrokenId,
    string? Detail);

/// <summary>Outcome of a PDF/A validation run (veraPDF).</summary>
public sealed record PdfaValidationResult(bool Validated, string? Note);

/// <summary>A generated PDF/A preservation master (or a note explaining why none was produced).</summary>
public sealed record PreservationCopyDto(
    bool Created,
    long? AttachmentId,
    long SourceAttachmentId,
    string? FileName,
    string? PdfAConformance,
    bool Validated,
    string? Note);

/// <summary>Repository preservation policy (ISO 16363).</summary>
public sealed record PreservationPolicyDto(
    string Name,
    string? Description,
    string TargetPdfAConformance,
    bool AutoNormalizeOnIngest,
    string FixityAlgorithm,
    int FixityCadenceDays,
    string? AllowedPreservationFormats);
