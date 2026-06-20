using Archiving.Domain.Common;
using Archiving.Domain.Enums;

namespace Archiving.Domain.Entities;

/// <summary>System account. Captures the mandated minimum: full name, email, phone, department, job title.</summary>
public class User : SoftDeleteEntity
{
    // Full display name — kept in sync with the name parts below.
    public string FullName { get; set; } = string.Empty;

    // Structured name components (Arabic names: اسم + اسم الأب + اسم الجد + اسم العائلة)
    public string? FirstName  { get; set; }
    public string? SecondName { get; set; }
    public string? ThirdName  { get; set; }
    public string? FamilyName { get; set; }

    public Gender Gender { get; set; } = Gender.NotSpecified;
    public string? NationalId { get; set; }

    public string Email { get; set; } = string.Empty;          // unique, login identifier
    public string? Phone { get; set; }
    public string JobTitle { get; set; } = string.Empty;
    public long? OrgUnitId { get; set; }                       // department the user belongs to
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Highest confidentiality the user may access (clearance).</summary>
    public ConfidentialityLevel Clearance { get; set; } = ConfidentialityLevel.Internal;

    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    // MFA
    public bool MfaEnabled { get; set; }
    public MfaMethod MfaMethod { get; set; } = MfaMethod.None;
    public string? MfaSecret { get; set; }                     // TOTP shared secret (encrypted at rest)

    // AD/LDAP linkage (Phase 1 integration)
    public string? DirectoryLogin { get; set; }

    // Navigation
    public OrgUnit? OrgUnit { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<PositionAssignment> PositionAssignments { get; set; } = new List<PositionAssignment>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}

public class Role : BaseEntity
{
    public string Name { get; set; } = string.Empty;          // unique
    public string? Description { get; set; }
    public bool IsSystem { get; set; }                         // seeded roles cannot be deleted

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}

/// <summary>A single (resource, action) capability, e.g. Documents.Delete.</summary>
public class Permission : BaseEntity
{
    public string Resource { get; set; } = string.Empty;      // module: Documents | IncomingMail | Workflow ...
    public PermissionAction Action { get; set; }
    public string Code { get; set; } = string.Empty;          // "Documents.Delete" — unique
    public string? Description { get; set; }
}

public class RolePermission
{
    public long RoleId { get; set; }
    public long PermissionId { get; set; }
    public Role Role { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

public class UserRole
{
    public long UserId { get; set; }
    public long RoleId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public User User { get; set; } = null!;
    public Role Role { get; set; } = null!;
}

/// <summary>A seat in the organization. Transactions bind to a Position, not a person —
/// so when the occupant changes, open work transfers automatically.</summary>
public class Position : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Code { get; set; }
    public long OrgUnitId { get; set; }
    public int Rank { get; set; }                              // seniority: higher = more senior (for escalation)
    public long? CurrentOccupantUserId { get; set; }          // who currently fills the seat (nullable = vacant)
    public bool IsActive { get; set; } = true;

    public OrgUnit OrgUnit { get; set; } = null!;
    public User? CurrentOccupant { get; set; }
    public ICollection<PositionAssignment> Assignments { get; set; } = new List<PositionAssignment>();
}

/// <summary>History of who held a position over time (for automatic hand-over of open transactions).</summary>
public class PositionAssignment : BaseEntity
{
    public long PositionId { get; set; }
    public long UserId { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public bool IsCurrent { get; set; } = true;

    public Position Position { get; set; } = null!;
    public User User { get; set; } = null!;
}

/// <summary>Issued JWT refresh tokens (rotation + revocation).</summary>
public class RefreshToken : BaseEntity
{
    public long UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? CreatedByIp { get; set; }
    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;

    public User User { get; set; } = null!;
}
