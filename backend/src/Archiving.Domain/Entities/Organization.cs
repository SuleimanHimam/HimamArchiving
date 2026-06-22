using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

public class Institution : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }                 // unique short code
    public string? LogoStorageKey { get; set; }       // logo stored in object storage, not the DB
    public string? LogoBase64    { get; set; }        // data-URI, stored in-DB for simplicity
    public string? ColorPrimary  { get; set; }        // hex for --ink CSS variable
    public string? ColorAccent   { get; set; }        // hex for --brass CSS variable
    public string? ColorSeal     { get; set; }        // hex for --seal CSS variable (alerts/stamps/seal buttons)
    public string? ColorBg       { get; set; }        // hex for --parchment CSS variable (page background)
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;
    public string? HiddenNavKeys { get; set; }        // CSV of navbar section keys hidden for all users

    public ICollection<OrgUnit> OrgUnits { get; set; } = new List<OrgUnit>();
}

/// <summary>Self-referencing node of the flexible org hierarchy
/// (institution → directorate → department → unit → committee → team).</summary>
public class OrgUnit : BaseEntity
{
    public long InstitutionId { get; set; }
    public long? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }
    public OrgUnitType Type { get; set; } = OrgUnitType.Department;
    public long? ManagerPositionId { get; set; }      // the seat that manages this unit (for DirectManager escalation)
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Institution Institution { get; set; } = null!;
    public OrgUnit? Parent { get; set; }
    public ICollection<OrgUnit> Children { get; set; } = new List<OrgUnit>();
    public ICollection<Position> Positions { get; set; } = new List<Position>();
}
