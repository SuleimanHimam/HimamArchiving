using Archiving.Domain.Enums;

namespace Archiving.Application.Features.Printing;

public sealed record ReportTemplateDto(
    long Id,
    long InstitutionId,
    string Scope,
    long? OrgUnitId,
    string? OrgUnitName,
    long? UserId,
    string? UserName,
    string Target,
    long? DocumentTypeId,
    string? DocumentTypeName,
    long? MailCategoryId,
    string? MailCategoryName,
    string Name,
    string? LogoBase64,
    string LayoutJson,
    bool IsActive,
    /// <summary>Whether this caller may edit this design. A design is only ever visible to its
    /// owner, its own department, or — for the institution defaults — everyone; so this is false
    /// for the defaults, which are the System Administrator's alone.</summary>
    bool CanEdit = true);

public sealed record SaveReportTemplateRequest(
    ReportTemplateScope Scope,
    long? OrgUnitId,
    long? UserId,
    ReportTemplateTarget Target,
    string Name,
    string? LogoBase64,
    string LayoutJson,
    bool IsActive = true,
    long? DocumentTypeId = null,
    long? MailCategoryId = null);

/// <summary>The design chosen for a print job after resolving User → OrgUnit → Default,
/// with the logo already falling back to the institution logo when the design has none.</summary>
public sealed record ResolvedReportTemplateDto(
    long? TemplateId,
    string Name,
    string? LogoBase64,
    string LayoutJson,
    string Source,    // User | OrgUnit | Default | None
    /// <summary>True when the chosen design was narrowed to the requested document type / mail
    /// category rather than being the scope's catch-all.</summary>
    bool MatchedType = false);
