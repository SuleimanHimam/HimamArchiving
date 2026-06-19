using Archiving.Domain.Common;

namespace Archiving.Domain.Entities;

/// <summary>Admin-managed confidentiality classification types (e.g. عام, سري, سري للغاية).</summary>
public class ClassificationType : BaseEntity
{
    public string NameAr    { get; set; } = string.Empty;
    public string? NameEn   { get; set; }
    public string? Description { get; set; }
    public string Color     { get; set; } = "#6b7280";
    public int    SortOrder { get; set; }
    public bool   IsSystem  { get; set; }
    public bool   IsActive  { get; set; } = true;

    public ICollection<RoleClassification> RoleClassifications { get; set; } = new List<RoleClassification>();
}

/// <summary>Which roles are permitted to use a given classification type.</summary>
public class RoleClassification
{
    public long RoleId               { get; set; }
    public long ClassificationTypeId { get; set; }

    public Archiving.Domain.Entities.Role Role               { get; set; } = null!;
    public ClassificationType              Classification     { get; set; } = null!;
}
