using Archiving.Domain.Entities;
using Archiving.Domain.Enums;

namespace Archiving.Infrastructure.Persistence;

/// <summary>
/// The single source of truth for what permissions exist and what each built-in role gets by default.
///
/// It is an explicit per-resource action list rather than a resource × action cartesian product: every
/// code below is enforced somewhere — a <c>[HasPermission]</c> attribute, a <c>User.HasClaim</c> check,
/// a <see cref="Archiving.Domain.StateMachine.DocumentLifecycle"/> transition, or a client-side gate in
/// the SPA (sidebar entries, <c>Scanner.*</c>). Anything not listed here grants nothing, so listing it
/// in the roles matrix would only invite an administrator to tick a box that does not do anything.
///
/// Both <see cref="DbSeeder"/> (startup seed) and the "reset to defaults" endpoint read this file, so
/// the two can no longer drift apart.
/// </summary>
public static class PermissionCatalog
{
    private static readonly PermissionAction V = PermissionAction.View;
    private static readonly PermissionAction C = PermissionAction.Create;
    private static readonly PermissionAction E = PermissionAction.Edit;
    private static readonly PermissionAction D = PermissionAction.Delete;
    private static readonly PermissionAction P = PermissionAction.Print;
    private static readonly PermissionAction A = PermissionAction.Archive;
    private static readonly PermissionAction Ap = PermissionAction.Approve;
    private static readonly PermissionAction F = PermissionAction.Forward;

    /// <summary>Resource → the actions that resource actually supports, in display order.</summary>
    public static readonly (string Resource, PermissionAction[] Actions)[] Resources =
    [
        // ---- Records & correspondence ----
        // Diwan (incoming/outgoing) + internal mail. No Approve: mail is referred and archived,
        // approval is a document-lifecycle step.
        ("Correspondence", [V, C, E, D, F, P, A]),
        ("Documents",      [V, C, E, D, Ap, P, A]),
        ("Notes",          [V, C]),
        ("Export",         [V]),

        // ---- Retention & disposal ----
        ("Archive",        [V, C, E, D, A]),             // physical archive: buildings, rooms, shelves, boxes
        ("Preservation",   [V, E]),                      // retention policies / file plan
        ("Disposition",    [V, C, E, Ap]),
        ("Destruction",    [V, C, D, Ap]),
        ("LegalHold",      [V, E]),

        // ---- Oversight ----
        ("Workflow",       [V, E]),                      // routing & escalation rules
        ("Audit",          [V, E, D]),

        // ---- System administration ----
        ("Users",          [V, C, E, D]),
        ("Organization",   [V, C, E, D]),                // org units, external directory
        // Print designs (الترويسات) for documents — the block designer. Correspondence no longer
        // uses this: it moved to the slot-based Letterhead resource below.
        ("PrintDesign",    [V, C, E, D]),
        // Slot-based letterhead for outgoing correspondence. Edit covers both the org-locked slots
        // and a department's slots; which of the two a caller may touch is decided by scope in
        // LetterheadService/Controller, not by a separate code. Approve clears a department's
        // pending edit. A user's own signature block needs nothing — it is their own record.
        ("Letterhead",     [V, E, Ap]),
        ("Settings",       [V, E]),                      // settings section, numbering, integration hooks
        ("Classification", [V, E]),
        ("CustomFields",   [E]),
        ("TableColumns",   [E]),
        ("Scanner",        [V, E]),                      // scanner/printer devices (enforced in the SPA)
        ("Backup",         [V, E]),
    ];

    /// <summary>Every valid permission code, e.g. "Documents.Archive".</summary>
    public static readonly HashSet<string> AllCodes =
        Resources.SelectMany(r => r.Actions.Select(a => $"{r.Resource}.{a}"))
                 .ToHashSet(StringComparer.Ordinal);

    /// <summary>Built-in roles and the permissions each one starts with.</summary>
    public static readonly (string Name, string Description, Func<Permission, bool> Pick)[] RoleDefaults =
    [
        ("System Administrator", "تحكم كامل بالنظام", _ => true),

        // Approvals and oversight. Reads everything except the system settings screen, which has to be
        // granted deliberately rather than arriving with a blanket "view", and except the physical
        // archive: قسم الأرشيف is the custodians' workspace (buildings, rooms, shelves, boxes), not an
        // oversight report. A manager who also runs the archive gets Archive.View granted explicitly.
        ("Manager", "اعتماد وتظهير المعاملات ومراجعة التقارير",
            p => (p.Action == PermissionAction.View && p.Resource is not ("Settings" or "Archive"))
                || (p.Resource == "Correspondence" && p.Action is PermissionAction.Create or PermissionAction.Edit
                    or PermissionAction.Forward or PermissionAction.Print or PermissionAction.Archive)
                || (p.Resource == "Documents" && p.Action is PermissionAction.Approve or PermissionAction.Print
                    or PermissionAction.Archive)
                // Heads of department maintain their own department's letterhead. The Edit code
                // only reaches department slots — org-locked slots additionally require the
                // System Administrator role, so a manager cannot rebrand the institution.
                || (p.Resource == "PrintDesign" && p.Action is PermissionAction.Create or PermissionAction.Edit)
                || (p.Resource == "Letterhead" && p.Action is PermissionAction.View or PermissionAction.Edit)
                // Legal / department head: the SECOND (final) approval step of disposition
                || (p.Resource == "Disposition" && p.Action == PermissionAction.Approve)
                // Managers may request, approve, and execute destruction directly
                || (p.Resource == "Destruction" && p.Action is PermissionAction.Create or PermissionAction.Delete
                    or PermissionAction.Approve)
                || (p.Resource is "LegalHold" or "Workflow" && p.Action == PermissionAction.Edit)
                // A department head curates their own department's monitoring log. AuditController
                // scopes both reading and hiding to their department, so this never reaches another
                // department's entries — and hiding is a soft-hide: the hash chain stays intact and
                // the act of hiding is itself audited.
                || (p.Resource == "Audit" && p.Action == PermissionAction.Delete)
                || (p.Resource == "Notes" && p.Action == PermissionAction.Create)
                /* A head staffs and maintains their own department: adding a person, changing what
                 * role they hold, activating or deactivating them, resetting a forgotten password.
                 * This is department-wide, not institution-wide, and UserAdminService is what makes
                 * that true — every write checks the target is inside the caller's departments,
                 * refuses an administrator's account outright, and refuses to grant the System
                 * Administrator role at all, so the scope cannot be escaped through the very
                 * screen that manages it.
                 *
                 * Role *definitions* and the permission matrix stay with the administrator, even
                 * though Users.Edit is the code that reaches them: a role is institution-wide, so a
                 * head editing one would be changing what every other department can do. The
                 * service refuses those four calls for anyone but the administrator. */
                || (p.Resource == "Users" && p.Action is PermissionAction.Create or PermissionAction.Edit)
                || (p.Resource is "Scanner" or "TableColumns" && p.Action == PermissionAction.Edit)),

        // Intake, classification and custody of the records themselves.
        ("Archive Officer", "إدخال وتصنيف وحفظ الوثائق وإدارة المواقع",
            p => ((p.Resource is "Documents" or "Archive") && p.Action != PermissionAction.Approve)
                || p.Resource is "Notes" or "Export" or "TableColumns" or "CustomFields"
                /* قسم الأرشيف corresponds like any other department: it writes to the department
                   whose record it just filed, asks for a missing attachment, answers a retrieval
                   request. Create covers composing and replying, Forward covers referring a letter
                   on — without them the archive could read, edit and close a letter but never
                   start one, and every message from the archive had to be sent by someone else. */
                || (p.Resource == "Correspondence" && p.Action is PermissionAction.View or PermissionAction.Create
                    or PermissionAction.Edit or PermissionAction.Forward or PermissionAction.Print
                    or PermissionAction.Archive)
                || (p.Resource is "PrintDesign" or "Letterhead" && p.Action == PermissionAction.View)
                || (p.Resource is "Scanner" or "Classification" && p.Action is PermissionAction.View or PermissionAction.Edit)
                || (p.Resource is "Preservation" or "LegalHold" && p.Action == PermissionAction.View)
                // Records officer: raises and VERIFIES (first step of) disposition requests
                || (p.Resource == "Disposition" && p.Action is PermissionAction.View or PermissionAction.Create
                    or PermissionAction.Edit)
                || (p.Resource == "Destruction" && p.Action is PermissionAction.View or PermissionAction.Create)),

        // Day-to-day correspondence: receive, reply, refer, follow up, and send to the archive.
        ("Employee", "استلام المعاملات والرد والإحالة والمتابعة",
            /* Correspondence.Archive alongside the rest: closing out a letter you have finished with
               is the last step of handling it, not an oversight act, and withholding it meant every
               employee had to ask a manager to press the button on their own completed work. It
               files an official copy into Documents (MailArchive) — which is bounded by the same
               department scope as everything else — and notifies the archive staff.

               Not to be confused with the Archive.* resource below: that is the physical archive,
               the shelves themselves, and it stays with the people who keep them. */
            p => (p.Resource == "Correspondence" && p.Action is PermissionAction.View or PermissionAction.Create
                    or PermissionAction.Edit or PermissionAction.Forward or PermissionAction.Print
                    or PermissionAction.Archive)
                || (p.Resource == "Documents" && p.Action is PermissionAction.View or PermissionAction.Create
                    or PermissionAction.Archive)
                // Enough to open the designer and see the letterhead they sign under. Editing
                // their own signature slots needs no permission — it is their own record.
                || (p.Resource is "PrintDesign" or "Letterhead" && p.Action == PermissionAction.View)
                // No Archive.* — the physical archive belongs to its own staff. An employee hands a
                // document over (Documents.Archive) and the archive files it; they never browse the
                // shelves. This is what keeps قسم الأرشيف off their sidebar.
                // Anyone holding a record may ASK for it to be destroyed or renewed; they can see
                // the queue and their own requests, and nothing else. Neither step of the approval
                // is theirs: verification needs Disposition.Edit (Archive Officer) and the final
                // word needs Disposition.Approve (Manager), and one person can never do both.
                || (p.Resource == "Disposition" && p.Action is PermissionAction.View or PermissionAction.Create)
                || (p.Resource is "Notes" or "Export" && p.Action is PermissionAction.View or PermissionAction.Create)
                || (p.Resource is "Scanner" && p.Action is PermissionAction.View or PermissionAction.Edit)
                || (p.Resource == "TableColumns" && p.Action == PermissionAction.Edit)),
    ];
}
