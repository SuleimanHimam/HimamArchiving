using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

public class Institution : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? Code { get; set; }                 // unique short code
    public string? LogoStorageKey { get; set; }       // logo stored in object storage, not the DB
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;

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
