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

    /// <summary>
    /// Profile picture as a self-contained data URI (data:image/...;base64,...).
    /// Held inline rather than in document storage: an avatar is small, is needed on
    /// every page render alongside the user, and has none of the retention, clearance
    /// or audit obligations that make the attachment pipeline the right home for a
    /// document. The API caps it at <see cref="AvatarMaxChars"/>.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>~400 KB of base64 — a 256px avatar is far below this in practice.</summary>
    public const int AvatarMaxChars = 400_000;

    /// <summary>
    /// Sidebar sections hidden for this person alone, as a CSV of nav keys.
    ///
    /// Distinct from <see cref="Institution.HiddenNavKeys"/>, which removes a module from the whole
    /// institution: this is a department head tidying one person's rail down to the work they
    /// actually do. The two are unioned, so a section hidden institution-wide stays hidden whatever
    /// is here.
    ///
    /// Hiding is presentation, never access — the permission still decides what the API answers.
    /// A section removed from someone's rail is one they had no business opening, not one they were
    /// able to open until now; anything that must actually be forbidden is a permission.
    /// </summary>
    public string? HiddenNavKeys { get; set; }

    /// <summary>
    /// The employee's handwritten signature (توقيع) and official stamp (ختم), each a data URI
    /// like <see cref="AvatarUrl"/>. A print design can carry a signature/stamp placeholder, and
    /// the letter is sealed with these when it is printed — so the same letterhead produces a
    /// correctly signed letter for whoever sent it.
    ///
    /// Stored inline for the same reasons as the avatar: small, needed at render time, and not a
    /// record in its own right. They are personal credentials of a sort, so only the owner sets
    /// them; the print path reads them but never exposes them in list responses.
    /// </summary>
    public string? SignatureUrl { get; set; }
    public string? StampUrl { get; set; }

    /// <summary>Same ceiling as the avatar. A transparent PNG signature sits well below it.</summary>
    public const int SealMaxChars = 400_000;

    /// <summary>
    /// The print design this user's outgoing letters are produced on.
    ///
    /// Design resolution is otherwise positional (User → OrgUnit → Default), which answers
    /// "what applies to me" but never "which of the ones available to me do I want". Two people
    /// in the same department legitimately write on different letterheads, and this is where that
    /// choice lives. Null means fall back to the positional resolution.
    ///
    /// No FK: a deleted design must not block deleting it, and a stale id resolves to the
    /// fallback on the next print rather than failing.
    /// </summary>
    public long? DefaultPrintTemplateId { get; set; }

    /// <summary>
    /// The same choice for internal messages (البريد الداخلي), which resolve against the
    /// Correspondence register. Kept as its own column rather than sharing one: the two registers
    /// draw from different pools of designs, so a single slot would silently unset the other
    /// register's choice every time someone picked a design for this one.
    /// </summary>
    public long? DefaultMailTemplateId { get; set; }

    // MFA
    public bool MfaEnabled { get; set; }
    public MfaMethod MfaMethod { get; set; } = MfaMethod.None;
    public string? MfaSecret { get; set; }                     // TOTP shared secret (encrypted at rest)

    // AD/LDAP linkage (Phase 1 integration)
    public string? DirectoryLogin { get; set; }

    // Navigation
    public OrgUnit? OrgUnit { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    /// <summary>Permissions given to this person directly, beyond what their roles carry.</summary>
    public ICollection<UserPermissionGrant> PermissionGrants { get; set; } = new List<UserPermissionGrant>();
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

/// <summary>
/// One permission given to one person, on top of whatever their roles carry.
///
/// Roles are the institution's vocabulary — what a «موظف» is, everywhere. A department head cannot
/// edit them (that would redefine the word for every other department), but they routinely need to
/// let one person in their department do one extra thing: this employee also prints, that one also
/// exports. Recording that as a per-person grant keeps the role definition intact and keeps the
/// exception visible on the person it applies to.
///
/// Additive only. There is no row that takes a role's permission away, deliberately: if a head
/// could strike one out per-person, two employees holding «موظف» would behave differently with
/// nothing in either record to explain why.
/// </summary>
public class UserPermissionGrant : BaseEntity
{
    public long UserId { get; set; }
    public long PermissionId { get; set; }

    /// <summary>Who granted it. A head may only pass on what they themselves hold, so this is the
    /// record of who was accountable for the exception.</summary>
    public long? GrantedByUserId { get; set; }
    public string? Note { get; set; }

    public User User { get; set; } = null!;
    public Permission Permission { get; set; } = null!;
}

/// <summary>
/// An extra department a user may act in, beyond the one <see cref="User.OrgUnitId"/> files them under.
///
/// A department head normally answers for one department, and every scoped query reads that single
/// column. But a manager is often given a second department to cover — an acting appointment, a
/// merged unit, a small section with no head of its own — and the alternative to recording that is
/// either moving them (losing their own department) or handing them the administrator role (losing
/// the scope entirely). Neither is acceptable, so the grant is its own record.
///
/// Only the System Administrator creates these: widening someone's reach is exactly the decision a
/// department head must not make for themselves.
/// </summary>
public class UserOrgUnitAccess : BaseEntity
{
    public long UserId { get; set; }
    public long OrgUnitId { get; set; }

    /// <summary>Who granted it, kept alongside the audit entry so the reason survives on the record
    /// the scope actually reads.</summary>
    public long? GrantedByUserId { get; set; }
    public string? Note { get; set; }

    public User User { get; set; } = null!;
    public OrgUnit OrgUnit { get; set; } = null!;
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
