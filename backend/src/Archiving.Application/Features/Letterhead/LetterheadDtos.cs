namespace Archiving.Application.Features.Letterhead;

/// <summary>
/// Slots the institution owns. Locked: only an Organization Admin may change them, and no
/// department or user can override any of it — this is the part that has to look identical on
/// every outgoing letter, which is the whole point of the restructure.
/// </summary>
public sealed record OrgSlots(
    string? LogoBase64,
    string BrandPrimaryHex,
    string BrandAccentHex,
    /// <summary>The only accent colours a department may pick from. A department accent outside
    /// this list is rejected server-side rather than quietly corrected.</summary>
    IReadOnlyList<string> ApprovedPalette,
    string? LegalFooterAr,
    string? LegalFooterEn,
    int MarginTopMm,
    int MarginBottomMm,
    int MarginInnerMm,
    int MarginOuterMm,
    string BaseFontFamily,
    int BaseFontPt,
    string PageSize)
{
    /// <summary>What a fresh institution starts from, and the fallback when no org row exists yet.
    /// IBM Plex Sans Arabic is the design system's own face and ships vendored with the API, so a
    /// letter renders the same on a developer's Windows box and in a Linux container.</summary>
    public static OrgSlots Default { get; } = new(
        LogoBase64: null,
        BrandPrimaryHex: "#14213D",
        BrandAccentHex: "#C8A24B",
        ApprovedPalette: ["#14213D", "#1B5776", "#24759F", "#177D89", "#7B2D6E", "#9B2226", "#C8A24B"],
        LegalFooterAr: null,
        LegalFooterEn: null,
        MarginTopMm: 18, MarginBottomMm: 18, MarginInnerMm: 16, MarginOuterMm: 16,
        BaseFontFamily: "IBM Plex Sans Arabic",
        BaseFontPt: 11,
        PageSize: "A4");
}

/// <summary>
/// Slots a department may change — deliberately two. Anything more and departments drift apart
/// again, which is the problem this module exists to solve. Edits go through approval before
/// they reach paper.
/// </summary>
public sealed record DepartmentSlots(
    string? DepartmentLine,
    /// <summary>Must be a member of the org's <see cref="OrgSlots.ApprovedPalette"/>.</summary>
    string? AccentHex);

/// <summary>
/// Slots an employee may change on their own letters. Identity only — nothing here can move,
/// recolour or restyle anything on the page.
/// </summary>
public sealed record SignerSlots(
    string? SignerName,
    string? SignerTitle,
    string? SignerContact);

/// <summary>The three levels merged for one letter, ready to render.</summary>
public sealed record ResolvedLetterhead(
    OrgSlots Org,
    DepartmentSlots Department,
    SignerSlots Signer)
{
    /// <summary>The accent actually used: the department's approved choice, else the org's.</summary>
    public string EffectiveAccentHex =>
        string.IsNullOrWhiteSpace(Department.AccentHex) ? Org.BrandAccentHex : Department.AccentHex!;
}

/// <summary>The letter content the master layout pours into its fixed zones. Body is plain text —
/// paragraphs split on newlines — so no department can smuggle formatting into the page.</summary>
public sealed record LetterContent(
    string RegisterNumber,
    string Subject,
    string MailDate,
    string? SentDate,
    string? RecipientOrg,
    string? AddresseeName,
    string? AddresseeTitle,
    string Body,
    string? Classification,
    string? FileNumber);

/// <summary>One row of the admin review list (legacy designs the migration could not map).</summary>
public sealed record LetterheadReviewItem(
    long Id,
    string Name,
    string Scope,
    string? OrgUnitName,
    string? UserName,
    string? ReviewNote,
    DateTime CreatedAt);

// ---- Requests ----

public sealed record SaveOrgSlotsRequest(OrgSlots Slots);

public sealed record SaveDepartmentSlotsRequest(DepartmentSlots Slots);

public sealed record SaveSignerSlotsRequest(SignerSlots Slots);

/// <summary>
/// Preview request. Everything is optional so the same endpoint serves the designer (slot values
/// not yet saved) and the compose screen (a letter not yet saved) — the preview is always the
/// real renderer, never a CSS approximation, so what is on screen is the PDF.
/// </summary>
public sealed record PreviewLetterheadRequest(
    OrgSlots? Org,
    DepartmentSlots? Department,
    SignerSlots? Signer,
    LetterContent? Content,
    long? OrgUnitId,
    long? UserId);
