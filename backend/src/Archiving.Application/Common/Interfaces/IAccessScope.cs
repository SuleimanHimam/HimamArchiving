namespace Archiving.Application.Common.Interfaces;

/// <summary>
/// Who the caller is allowed to see records *about*, as opposed to what actions they may perform.
///
/// Permissions answer "may this user list documents at all"; this answers "which documents".
/// Both gates apply: a user needs the permission to reach a list, and then sees only the rows
/// this scope leaves them — their own work, their department's, and correspondence addressed
/// to them. Clearance is a third, independent filter and is applied by the services themselves.
///
/// Resolved once per request (the implementation is scoped), so the org-unit and role lookups
/// behind it do not repeat per query.
/// </summary>
public interface IAccessScope
{
    /// <summary>The System Administrator role — the only account that sees the whole institution,
    /// including other people's correspondence. Every other role is scoped, however many
    /// permissions it holds.</summary>
    Task<bool> IsSystemAdminAsync(CancellationToken ct = default);

    /// <summary>Whether the caller holds a permission code. System Administrator holds everything.</summary>
    Task<bool> HasPermissionAsync(string code, CancellationToken ct = default);

    /// <summary>The caller's own department — the one they are filed under, and the one anything
    /// they create is filed under. Null if they belong to none, in which case "my department's
    /// records" is empty rather than "everyone's".
    ///
    /// This is the caller's *home*, not the extent of what they may see: a manager covering a
    /// second department still has exactly one home. Read <see cref="OrgUnitIdsAsync"/> to decide
    /// what a query returns; read this to decide where a new record belongs.</summary>
    Task<long?> OrgUnitIdAsync(CancellationToken ct = default);

    /// <summary>Every department the caller acts in: their own, plus any granted to them by the
    /// System Administrator. This is what "my department" means to a list query.
    ///
    /// Empty when the caller belongs to no department and has been granted none — which filters
    /// everything out, deliberately.</summary>
    Task<IReadOnlyList<long>> OrgUnitIdsAsync(CancellationToken ct = default);

    /// <summary>The departments the caller *heads*, as opposed to merely belongs to.
    ///
    /// The distinction is what separates an employee from a manager inside the same department:
    /// both are in it, but only the head reads their colleagues' correspondence and their activity
    /// in the log. A department is headed by the occupant of its manager seat, and every explicitly
    /// granted department is headed too — the grant exists to put someone in charge of it.
    ///
    /// Empty for an ordinary employee, and for the administrator (who is not scoped at all).</summary>
    Task<IReadOnlyList<long>> ManagedOrgUnitIdsAsync(CancellationToken ct = default);

    /// <summary>Whether the caller sees every document in the institution: the administrator, and
    /// the archive custodians (<c>Archive.Archive</c>), who physically hold the records of every
    /// department and cannot file what they cannot see.</summary>
    Task<bool> SeesAllDocumentsAsync(CancellationToken ct = default);
}
