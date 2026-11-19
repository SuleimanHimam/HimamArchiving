using Archiving.Domain.Common;

namespace Archiving.Domain.Core;

/// <summary>
/// Base for every table in the shared core and the modules built on it (HR, supply, …).
/// Adds what the platform standard requires on top of the archive's <see cref="SoftDeleteEntity"/>:
/// a concurrency token, so two editors cannot silently overwrite each other.
/// </summary>
public abstract class CoreEntity : SoftDeleteEntity
{
    /// <summary>Optimistic-concurrency token (<c>row_version</c>). Database-generated on SQL Server.</summary>
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>Employment category. It selects which payroll, leave and promotion rules apply.</summary>
public enum EmployeeCategory
{
    Military = 0,   // عسكري
    Civil = 1,      // مدني
    Contract = 2,   // عقد
}

/// <summary>
/// Where the employee stands. Changed only by an approved personnel action (suspension, end of
/// service), never by editing the record — which is why the save request carries no status.
/// </summary>
public enum EmployeeStatus
{
    Active = 0,      // على رأس عمله
    Suspended = 1,   // موقوف عن العمل
    Terminated = 2,  // منتهية خدمته
}

public enum SiteLocationKind
{
    Office = 0,
    Gate = 1,
    Parking = 2,
    Warehouse = 3,
    Other = 9,
}

/// <summary>A crossing or headquarters: the place people work, shifts are rostered and devices sit.</summary>
public class Site : CoreEntity
{
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Code { get; set; } = string.Empty;    // unique short code
    public bool IsActive { get; set; } = true;

    public ICollection<SiteLocation> Locations { get; set; } = new List<SiteLocation>();
}

/// <summary>A place inside a site: a gate, a car park, a warehouse.</summary>
public class SiteLocation : CoreEntity
{
    public long SiteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Code { get; set; } = string.Empty;    // unique within the site
    public SiteLocationKind Kind { get; set; } = SiteLocationKind.Office;
    public bool IsActive { get; set; } = true;

    public Site Site { get; set; } = null!;
}

/// <summary>
/// The one employee record every module points at by id. It is defined here, once; HR adds the rest
/// of the personnel file in its own tables and never copies these columns.
///
/// <para>National id and mobile number are deliberately absent. The standard requires both to be
/// encrypted at column level, and the mechanism (Always Encrypted vs. application-level) is not yet
/// decided — so they are left out rather than stored in the clear.</para>
/// </summary>
public class Employee : CoreEntity
{
    public string EmployeeNumber { get; set; } = string.Empty;   // unique, assigned by HR
    public string FullName { get; set; } = string.Empty;
    public string? FullNameEn { get; set; }
    public EmployeeCategory Category { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    public long OrgUnitId { get; set; }          // the one organisational unit the employee belongs to
    public long? PositionId { get; set; }        // the seat, when the organisation models it
    public string JobTitle { get; set; } = string.Empty;
    public long? SiteId { get; set; }            // place of work
    public DateOnly? HireDate { get; set; }

    /// <summary>The login account, if one exists. One account per employee at most.</summary>
    public long? UserId { get; set; }

    public Site? Site { get; set; }
}
