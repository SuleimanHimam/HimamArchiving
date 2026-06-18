namespace Archiving.Application.Features.Preservation;

public sealed record RepresentationDto(
    long AttachmentId,
    string FileName,
    string? FormatName,
    string? MimeType,
    string? PronomPuid,
    string? RenderingNote);

public sealed record PackageFileDto(
    long AttachmentId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string? Checksum,
    string Algorithm,
    string Kind,
    string? PronomPuid);

public sealed record InformationPackageDto(
    long Id,
    string Type,            // SIP | AIP | DIP
    long DocumentId,
    int FileCount,
    long TotalBytes,
    DateTime CreatedAt,
    IReadOnlyList<PackageFileDto> Files);

public sealed record DocumentPackagesDto(
    long DocumentId,
    string DocumentNumber,
    string Title,
    InformationPackageDto? Sip,
    InformationPackageDto? Aip,
    IReadOnlyList<InformationPackageDto> Dips,
    IReadOnlyList<RepresentationDto> Representation);

public sealed record DesignatedCommunityDto(string Name, string? Description, string? RenderingExpectations);

/// <summary>A built AIP bundle (ZIP of preservation files + manifest.json).</summary>
public sealed record AipExport(byte[] Content, string FileName);
