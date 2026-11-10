using Archiving.Api.Authorization;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Printing;
using Archiving.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers;

[ApiController]
[Route("api/report-templates")]
[Authorize]
public sealed class ReportTemplatesController(
    IReportTemplateService service,
    IAuthorizationService authz,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>The saved designs, for the designer's "edit an existing design" picker. Browsing the
    /// catalogue needs <c>PrintDesign.View</c>; resolving one for an actual print job does not, so
    /// printing still works for staff who may never open the designer.</summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] ReportTemplateScope? scope,
        [FromQuery] ReportTemplateTarget? target,
        [FromQuery] long? documentTypeId,
        [FromQuery] long? mailCategoryId,
        CancellationToken ct)
    {
        if (!await HasAnyAsync("PrintDesign.View")) return Forbid();
        return Ok(await service.ListAsync(scope, target, documentTypeId, mailCategoryId, ct));
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        if (!await HasAnyAsync("PrintDesign.View")) return Forbid();
        var r = await service.GetAsync(id, ct);
        return r.Succeeded ? Ok(r.Value) : NotFound(new { error = r.Error });
    }

    /// <summary>Resolve the design that applies to a print job (User → OrgUnit → Default), preferring a
    /// design narrowed to the document type / mail category being printed. Omitting
    /// <c>orgUnitId</c> lets the service take it from the user's own department.</summary>
    [HttpGet("resolve")]
    public async Task<IActionResult> Resolve(
        [FromQuery] ReportTemplateTarget target = ReportTemplateTarget.All,
        [FromQuery] long? orgUnitId = null,
        [FromQuery] long? userId = null,
        [FromQuery] long? documentTypeId = null,
        [FromQuery] long? mailCategoryId = null,
        CancellationToken ct = default)
        => Ok(await service.ResolveAsync(target, orgUnitId, userId, documentTypeId, mailCategoryId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveReportTemplateRequest req, CancellationToken ct)
    {
        if (!await CanManageAsync(req, "PrintDesign.Create")) return Forbid();
        var r = await service.CreateAsync(req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveReportTemplateRequest req, CancellationToken ct)
    {
        if (!await CanManageAsync(req, "PrintDesign.Edit")) return Forbid();
        var r = await service.UpdateAsync(id, req, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var existing = await service.GetAsync(id, ct);
        if (!existing.Succeeded) return NotFound(new { error = existing.Error });
        // Deleting a personal design needs no admin right; department/default designs do.
        var isOwnUser = existing.Value!.Scope == nameof(ReportTemplateScope.User)
            && existing.Value.UserId == currentUser.UserId;
        if (!isOwnUser && !await HasAnyAsync("PrintDesign.Delete")) return Forbid();
        var r = await service.DeleteAsync(id, ct);
        return r.Succeeded ? NoContent() : BadRequest(new { error = r.Error });
    }

    /// <summary>A user may always manage their own personal design. Department-wide and
    /// institution-wide designs need the matching PrintDesign right.</summary>
    private async Task<bool> CanManageAsync(SaveReportTemplateRequest req, string permission)
    {
        if (req.Scope == ReportTemplateScope.User && req.UserId == currentUser.UserId) return true;
        return await HasAnyAsync(permission);
    }

    /// <summary>True when the caller holds the given PrintDesign right, or the legacy
    /// <c>Organization.Edit</c> that governed designs before they had their own resource — so roles
    /// configured against the old rule keep working until they are re-granted.</summary>
    private async Task<bool> HasAnyAsync(string permission)
        => (await authz.AuthorizeAsync(User, null, new PermissionRequirement(permission))).Succeeded
           || (await authz.AuthorizeAsync(User, null, new PermissionRequirement("Organization.Edit"))).Succeeded;
}
