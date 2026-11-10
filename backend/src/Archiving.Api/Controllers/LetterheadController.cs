using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Letterhead;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

/// <summary>
/// Slot-based letterhead for outgoing correspondence.
///
/// Three levels, three different authorization rules — and they are deliberately not three
/// permission codes. <c>Letterhead.Edit</c> only says "may edit some letterhead"; <em>which</em>
/// level a caller reaches is decided here, because the answer depends on who they are relative
/// to the department, which a static permission cannot express.
/// </summary>
[ApiController]
[Route("api/letterhead")]
[Authorize]
public sealed class LetterheadController(
    ILetterheadService service,
    AppDbContext db,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>"Organization Admin" in the requirements maps onto the institution's existing
    /// top-level role rather than introducing a second one that would need its own seeding.</summary>
    private const string OrgAdminRole = PermissionAuthorizationHandler.AdminRole;

    // ── Read ─────────────────────────────────────────────────────────────────────

    /// <summary>The merged letterhead the caller signs under, plus each level's own slots so the
    /// designer can show what is inherited and what is editable.</summary>
    [HttpGet("profile")]
    [HasPermission("Letterhead.View")]
    public async Task<IActionResult> Profile([FromQuery] long? orgUnitId, [FromQuery] long? userId, CancellationToken ct)
    {
        var targetUserId = userId ?? currentUser.UserId;
        var targetUnitId = orgUnitId ?? await OwnOrgUnitIdAsync(ct);

        return Ok(new
        {
            org = await service.GetOrgSlotsAsync(ct),
            department = targetUnitId is { } u ? await service.GetDepartmentSlotsAsync(u, ct) : null,
            signer = targetUserId is { } uid ? await service.GetSignerSlotsAsync(uid, ct) : null,
            orgUnitId = targetUnitId,
            // The SPA renders read-only fields rather than hiding them, so inheritance is visible
            // instead of looking like a missing feature.
            canEditOrg = User.IsInRole(OrgAdminRole),
            canEditDepartment = targetUnitId is not null && await CanEditDepartmentAsync(targetUnitId.Value, ct),
            pendingApproval = targetUnitId is { } pu && await HasPendingAsync(pu, ct),
        });
    }

    /// <summary>Legacy designs the migration could not map onto slots. Admin review list —
    /// nothing here was deleted, it is all still in LegacyLayoutJson.</summary>
    [HttpGet("review")]
    [HasPermission("Letterhead.Edit")]
    public async Task<IActionResult> Review(CancellationToken ct)
        => Ok(await service.ListNeedsReviewAsync(ct));

    // ── Write ────────────────────────────────────────────────────────────────────

    /// <summary>Organization-locked slots. Requires the Organization Admin role on top of the
    /// permission: these are the values that must be identical on every letter, so a department
    /// manager holding <c>Letterhead.Edit</c> must not reach them.</summary>
    [HttpPut("org")]
    [HasPermission("Letterhead.Edit")]
    public async Task<IActionResult> SaveOrg([FromBody] SaveOrgSlotsRequest req, CancellationToken ct)
    {
        if (!User.IsInRole(OrgAdminRole))
            return Forbid();

        var result = await service.SaveOrgSlotsAsync(req.Slots, ct);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    /// <summary>Department slots. Saved as pending and routed through the approval workflow —
    /// the response deliberately reports that it is not yet live.</summary>
    [HttpPut("department/{orgUnitId:long}")]
    [HasPermission("Letterhead.Edit")]
    public async Task<IActionResult> SaveDepartment(
        long orgUnitId, [FromBody] SaveDepartmentSlotsRequest req, CancellationToken ct)
    {
        if (!await CanEditDepartmentAsync(orgUnitId, ct))
            return Forbid();

        var result = await service.SaveDepartmentSlotsAsync(orgUnitId, req.Slots, ct);
        return result.Succeeded
            ? Ok(new { slots = result.Value, pendingApproval = true })
            : BadRequest(new { error = result.Error });
    }

    /// <summary>A user's own signature block. No permission required and no approval: it is their
    /// own name, and it cannot change how anything on the page looks.</summary>
    [HttpPut("user")]
    public async Task<IActionResult> SaveSigner([FromBody] SaveSignerSlotsRequest req, CancellationToken ct)
    {
        if (currentUser.UserId is not { } userId) return Forbid();
        var result = await service.SaveSignerSlotsAsync(userId, req.Slots, ct);
        return result.Succeeded ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    // ── Preview ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Renders a preview through the real renderer and returns the actual PDF. Not a CSS
    /// approximation: the bytes here are produced by the same code path that prints the final
    /// letter, so the designer cannot show something the printer will not.
    /// </summary>
    [HttpPost("preview")]
    [HasPermission("Letterhead.View")]
    public async Task<IActionResult> Preview([FromBody] PreviewLetterheadRequest req, CancellationToken ct)
    {
        // Unsaved org slots are only honoured for someone who could actually save them —
        // otherwise the preview would be a way to see the institution rebranded.
        var request = User.IsInRole(OrgAdminRole) ? req : req with { Org = null };

        var result = await service.RenderPreviewAsync(request, ct);
        return result.Succeeded
            ? File(result.Value!, "application/pdf", "letterhead-preview.pdf")
            : BadRequest(new { error = result.Error });
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private async Task<long?> OwnOrgUnitIdAsync(CancellationToken ct)
        => currentUser.UserId is { } id
            ? await db.Users.Where(u => u.Id == id).Select(u => u.OrgUnitId).FirstOrDefaultAsync(ct)
            : null;

    /// <summary>
    /// A department's letterhead belongs to whoever manages that department. The Organization
    /// Admin may edit any; a manager may edit only the unit whose manager seat they occupy —
    /// belonging to a unit is not the same as running it.
    /// </summary>
    private async Task<bool> CanEditDepartmentAsync(long orgUnitId, CancellationToken ct)
    {
        if (User.IsInRole(OrgAdminRole)) return true;
        if (!User.HasClaim("permission", "Letterhead.Edit")) return false;
        if (currentUser.UserId is not { } userId) return false;

        var managerPositionId = await db.OrgUnits
            .Where(o => o.Id == orgUnitId).Select(o => o.ManagerPositionId).FirstOrDefaultAsync(ct);
        if (managerPositionId is null) return false;

        return await db.PositionAssignments
            .AnyAsync(a => a.PositionId == managerPositionId && a.UserId == userId && a.IsCurrent, ct);
    }

    private async Task<bool> HasPendingAsync(long orgUnitId, CancellationToken ct)
        => await db.ReportTemplates.AnyAsync(t =>
            t.OrgUnitId == orgUnitId
            && t.Target == Domain.Enums.ReportTemplateTarget.Correspondence
            && t.Status == Domain.Enums.LetterheadStatus.PendingApproval, ct);
}
