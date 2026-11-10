using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>An external party (ministry, company, authority, …) that correspondence can be sent to or
/// received from. New in v1.1 (EXT-01). Bilingual name, type, code, status.</summary>
public class ExternalOrganization : SoftDeleteEntity
{
    public string Name { get; set; } = string.Empty;        // Arabic / primary
    public string? NameEn { get; set; }
    public ExternalOrgType Type { get; set; } = ExternalOrgType.Other;
    public string? Code { get; set; }                        // optional org-scoped code (unique when present)
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    /// <summary>
    /// The organisation's logo, inline as a data URI — the same shape a user's avatar takes.
    ///
    /// Inline rather than a link: a remote address here would have the directory fetch an image
    /// from a host of someone else's choosing every time the page is drawn, and would leave a
    /// broken square behind the day that host goes away. Capped at
    /// <see cref="LogoMaxChars"/>, which is a generous logo and a poor way to store a photograph.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>~300 KB of base64 — about 220 KB of image. Matches the avatar cap's reasoning:
    /// large enough for any crest, small enough that a row stays a row.</summary>
    public const int LogoMaxChars = 400_000;

    public ICollection<ExternalContact> Contacts { get; set; } = new List<ExternalContact>();
}

/// <summary>A contact person belonging to an external organization, or standalone (EXT-02).</summary>
public class ExternalContact : SoftDeleteEntity
{
    public long? OrganizationId { get; set; }                // null = standalone contact
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Title { get; set; }                       // job title / role
    public bool IsActive { get; set; } = true;

    public ExternalOrganization? Organization { get; set; }
    public ICollection<ContactChannel> Channels { get; set; } = new List<ContactChannel>();
}

/// <summary>A communication channel for a contact: email/phone/address/fax, with a primary designation (EXT-03).</summary>
public class ContactChannel : BaseEntity
{
    public long ContactId { get; set; }
    public ContactChannelType Type { get; set; } = ContactChannelType.Email;
    public string Value { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }

    public ExternalContact Contact { get; set; } = null!;
}
