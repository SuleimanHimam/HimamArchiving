using Archiving.Domain.Enums;

namespace Archiving.Application.Features.External;

public sealed record ExternalOrgDto(
    long Id, string Name, string? NameEn, string Type, string? Code, bool IsActive, string? Notes, int ContactCount,
    /// <summary>Logo as a data URI, or null. Sent with the list: the directory draws it on every
    /// card, and a second round trip per organisation to fetch it would be slower than the bytes.</summary>
    string? LogoUrl = null);

public sealed record CreateExternalOrgDto(
    string Name, string? NameEn, ExternalOrgType Type, string? Code, string? Notes,
    /// <summary>A <c>data:image/…</c> URI to set the logo, or null to leave it/clear it.</summary>
    string? LogoUrl = null);

public sealed record ContactChannelDto(long Id, string Type, string Value, bool IsPrimary);

public sealed record CreateContactChannelDto(ContactChannelType Type, string Value, bool IsPrimary);

public sealed record ExternalContactDto(
    long Id, long? OrganizationId, string? OrganizationName, string Name, string? NameEn,
    string? Title, bool IsActive, IReadOnlyList<ContactChannelDto> Channels);

public sealed record CreateExternalContactDto(
    long? OrganizationId, string Name, string? NameEn, string? Title,
    IReadOnlyList<CreateContactChannelDto>? Channels);
