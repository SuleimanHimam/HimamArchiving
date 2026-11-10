using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>A print / report design (letterhead) used when printing correspondence or documents.
/// Designs are layered: a user's own design overrides their department's, which overrides the
/// institution-wide default. The <see cref="LayoutJson"/> holds the ordered designer blocks
/// (logo, header, fields, table, text, footer …) authored in the report designer.
///
/// A design may also be narrowed to one kind of item: a document type when
/// <see cref="Target"/> is Document, or a mail category when it is Correspondence. That is
/// what lets the Finance department print salary letters on a different letterhead from its
/// contracts. A design left un-narrowed applies to every kind at its scope, and a narrowed
/// design always wins over it.</summary>
public class ReportTemplate : BaseEntity
{
    public long InstitutionId { get; set; }
    public ReportTemplateScope Scope { get; set; } = ReportTemplateScope.Default;
    public long? OrgUnitId { get; set; }              // set when Scope = OrgUnit
    public long? UserId { get; set; }                 // set when Scope = User
    public ReportTemplateTarget Target { get; set; } = ReportTemplateTarget.All;

    /// <summary>Narrows the design to one document type (Target = Document). null = every type.</summary>
    public long? DocumentTypeId { get; set; }
    /// <summary>Narrows the design to one mail category (Target = Correspondence). null = every category.</summary>
    public long? MailCategoryId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? LogoBase64 { get; set; }           // uploaded logo (data URI); falls back to the institution logo
    public string LayoutJson { get; set; } = "[]";    // ordered designer blocks — Target = Document only
    public bool IsActive { get; set; } = true;

    // ---- Slot-based letterhead (Target = Correspondence) ----
    // Correspondence no longer owns the canvas. A fixed QuestPDF master layout owns the page and
    // these templates only fill the slots their scope is allowed to touch, which is what makes
    // every department's outgoing letter recognisably the same institution's.

    /// <summary>Slot values for this level, serialized. The shape depends on <see cref="Scope"/>:
    /// Default → OrgSlots (logo, brand colours, approved palette, legal footer, margins, base font),
    /// OrgUnit → DepartmentSlots (department line, accent from the approved palette),
    /// User → SignerSlots (signer name, title, contact). Empty for Target = Document.</summary>
    public string? SlotsJson { get; set; }

    public LetterheadStatus Status { get; set; } = LetterheadStatus.Draft;

    /// <summary>The original free-form design, preserved verbatim when this row was migrated onto
    /// slots. Never rendered and never cleared — it is the only record of what an admin authored
    /// before the restructure, and a flagged row is re-authored by reading it.</summary>
    public string? LegacyLayoutJson { get; set; }

    /// <summary>Why the migration could not map this design, shown on the admin review list.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>The approval run for the pending department edit, if any.</summary>
    public long? ApprovalWorkflowInstanceId { get; set; }

    public Institution Institution { get; set; } = null!;
    public OrgUnit? OrgUnit { get; set; }
}
