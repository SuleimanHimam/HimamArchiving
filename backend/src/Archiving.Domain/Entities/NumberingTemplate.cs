using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>A configurable auto-numbering pattern for an entity type, scoped from global down to a specific
/// org-unit + document type. Tokens: {YYYY} {MM} {DEPT} {TYPE} {INST} {SEQ} {SEQ:N}.</summary>
public class NumberingTemplate : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;   // Document | IncomingMail | OutgoingMail | Correspondence | …
    public long? InstitutionId { get; set; }                  // null = global
    public long? OrgUnitId { get; set; }                      // null = institution-wide
    public long? DocumentTypeId { get; set; }                 // null = all types
    public string Pattern { get; set; } = string.Empty;       // e.g. "{DEPT}-{TYPE}-{YYYY}-{SEQ:5}"
    public ResetFrequency ResetFrequency { get; set; } = ResetFrequency.Yearly;
    public int CurrentSequence { get; set; }
    public int? CurrentYear { get; set; }
    public int? CurrentMonth { get; set; }
    public bool IsActive { get; set; } = true;
}
