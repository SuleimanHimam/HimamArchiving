using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Printing;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class ReportTemplateService(
    AppDbContext db, IAuditWriter audit, ICurrentUser currentUser, IAccessScope scope) : IReportTemplateService
{
    private const string EntityType = "ReportTemplate";

    /// <summary>Which designs exist, as far as this caller is concerned.
    ///
    /// A design at User scope is that person's own stationery — their name, title and signature
    /// block — so it is theirs to see and nobody else's. A department's designs stay inside the
    /// department; another department's are not visible at all, whatever rights the caller holds
    /// over designs in general. The institution defaults are visible to everyone because everyone
    /// prints under them.
    ///
    /// There is no way past that line — no flag publishes one department's design to another.
    /// Something meant for the whole institution is authored at
    /// <see cref="ReportTemplateScope.Default"/>, which is what every department already falls
    /// back to when it has no design of its own.
    ///
    /// This used to let every holder of <c>PrintDesign.Edit</c> see every design — which put each
    /// department's letterheads, and each person's personal stationery, in front of every other
    /// department's manager.
    ///
    /// Resolving a design for an actual print job deliberately does NOT go through here — printing
    /// must keep working for staff who never open the designer.</summary>
    private async Task<IQueryable<ReportTemplate>> VisibleAsync(IQueryable<ReportTemplate> q, CancellationToken ct)
    {
        if (await scope.IsSystemAdminAsync(ct)) return q;
        var me = currentUser.UserId;
        var depts = await scope.OrgUnitIdsAsync(ct);
        return q.Where(t => t.Scope == ReportTemplateScope.Default
            || (t.Scope == ReportTemplateScope.User && t.UserId == me)
            || (t.Scope == ReportTemplateScope.OrgUnit && t.OrgUnitId != null && depts.Contains(t.OrgUnitId.Value)));
    }

    /// <summary>Where this caller may put a design, with the reason if nowhere.
    ///
    /// Visibility decides what they can open; this decides what they can author. The two must
    /// agree, or a manager saves a design into a department they cannot then see. Institution-wide
    /// defaults are the administrator's: they are what every department falls back to.</summary>
    private async Task<string?> WriteDenyReasonAsync(SaveReportTemplateRequest r, CancellationToken ct)
    {
        if (await scope.IsSystemAdminAsync(ct)) return null;
        return r.Scope switch
        {
            ReportTemplateScope.Default => "التصميم الافتراضي للمؤسسة يحدده مدير النظام",
            ReportTemplateScope.User when r.UserId != currentUser.UserId => "لا يمكن تعديل تصميم مستخدم آخر",
            ReportTemplateScope.OrgUnit when r.OrgUnitId is not { } unit
                || !(await scope.OrgUnitIdsAsync(ct)).Contains(unit) => "لا يمكن حفظ تصميم لقسم آخر",
            _ => null,
        };
    }

    /// <summary>Whether the caller may change this stored design — the same question as
    /// <see cref="WriteDenyReasonAsync"/>, asked of what is on the row rather than what is being
    /// sent.</summary>
    private async Task<bool> CanEditAsync(ReportTemplate t, CancellationToken ct)
    {
        if (await scope.IsSystemAdminAsync(ct)) return true;
        return t.Scope switch
        {
            ReportTemplateScope.User => t.UserId == currentUser.UserId,
            ReportTemplateScope.OrgUnit => t.OrgUnitId is { } unit && (await scope.OrgUnitIdsAsync(ct)).Contains(unit),
            _ => false,
        };
    }

    public async Task<IReadOnlyList<ReportTemplateDto>> ListAsync(
        ReportTemplateScope? scope, ReportTemplateTarget? target,
        long? documentTypeId = null, long? mailCategoryId = null, CancellationToken ct = default)
    {
        var q = db.ReportTemplates.AsQueryable();
        if (scope is { } s) q = q.Where(t => t.Scope == s);
        if (target is { } tg) q = q.Where(t => t.Target == tg);
        // Filtering by a type shows what would apply to it: its own designs plus the catch-alls.
        if (documentTypeId is { } dt) q = q.Where(t => t.DocumentTypeId == null || t.DocumentTypeId == dt);
        if (mailCategoryId is { } mc) q = q.Where(t => t.MailCategoryId == null || t.MailCategoryId == mc);
        q = await VisibleAsync(q, ct);
        var list = await q.OrderBy(t => t.Scope).ThenBy(t => t.Name).ToListAsync(ct);
        return await ToDtosAsync(list, ct);
    }

    public async Task<Result<ReportTemplateDto>> GetAsync(long id, CancellationToken ct = default)
    {
        // Same visibility as the gallery — otherwise a design hidden from the list opens by id.
        var e = await (await VisibleAsync(db.ReportTemplates, ct)).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (e is null) return Result<ReportTemplateDto>.Fail("التصميم غير موجود");
        return Result<ReportTemplateDto>.Ok((await ToDtosAsync([e], ct))[0]);
    }

    public async Task<Result<ReportTemplateDto>> CreateAsync(SaveReportTemplateRequest r, CancellationToken ct = default)
    {
        var err = Validate(r) ?? await WriteDenyReasonAsync(r, ct);
        if (err is not null) return Result<ReportTemplateDto>.Fail(err);
        var institutionId = await DefaultInstitutionIdAsync(ct);
        if (institutionId is null) return Result<ReportTemplateDto>.Fail("لا توجد مؤسسة مُعرّفة");

        var e = new ReportTemplate
        {
            InstitutionId = institutionId.Value,
            Scope = r.Scope,
            OrgUnitId = r.Scope == ReportTemplateScope.OrgUnit ? r.OrgUnitId : null,
            UserId = r.Scope == ReportTemplateScope.User ? r.UserId : null,
            Target = r.Target,
            DocumentTypeId = NarrowedDocumentType(r),
            MailCategoryId = NarrowedMailCategory(r),
            Name = r.Name.Trim(),
            LogoBase64 = r.LogoBase64,
            LayoutJson = string.IsNullOrWhiteSpace(r.LayoutJson) ? "[]" : r.LayoutJson,
            IsActive = r.IsActive,
        };
        db.ReportTemplates.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", EntityType, e.Id, e.Name, ct: ct);
        return Result<ReportTemplateDto>.Ok((await ToDtosAsync([e], ct))[0]);
    }

    public async Task<Result<ReportTemplateDto>> UpdateAsync(long id, SaveReportTemplateRequest r, CancellationToken ct = default)
    {
        // Nobody edits a design they cannot see. The controller decides who may *save* what
        // (own personal design freely, department/default only with the right); this decides
        // which designs exist as far as the caller is concerned.
        var e = await (await VisibleAsync(db.ReportTemplates, ct)).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (e is null) return Result<ReportTemplateDto>.Fail("التصميم غير موجود");
        // Both ends are checked: what the design is now, and what it is being turned into. Without
        // the first, an institution default is editable by anyone who can see it; without the
        // second, editing is a way to move a design into a department at will.
        if (!await CanEditAsync(e, ct)) return Result<ReportTemplateDto>.Fail("لا تملك صلاحية تعديل هذا التصميم");
        var err = Validate(r) ?? await WriteDenyReasonAsync(r, ct);
        if (err is not null) return Result<ReportTemplateDto>.Fail(err);

        e.Scope = r.Scope;
        e.OrgUnitId = r.Scope == ReportTemplateScope.OrgUnit ? r.OrgUnitId : null;
        e.UserId = r.Scope == ReportTemplateScope.User ? r.UserId : null;
        e.Target = r.Target;
        e.DocumentTypeId = NarrowedDocumentType(r);
        e.MailCategoryId = NarrowedMailCategory(r);
        e.Name = r.Name.Trim();
        e.LogoBase64 = r.LogoBase64;
        e.LayoutJson = string.IsNullOrWhiteSpace(r.LayoutJson) ? "[]" : r.LayoutJson;
        e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Updated", EntityType, e.Id, e.Name, ct: ct);
        return Result<ReportTemplateDto>.Ok((await ToDtosAsync([e], ct))[0]);
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        // Deleting followed neither visibility nor ownership: any id reachable by guessing was
        // removable, including another department's letterhead.
        var e = await (await VisibleAsync(db.ReportTemplates, ct)).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (e is null) return Result<bool>.Fail("التصميم غير موجود");
        if (!await CanEditAsync(e, ct)) return Result<bool>.Fail("لا تملك صلاحية حذف هذا التصميم");
        db.ReportTemplates.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", EntityType, id, e.Name, ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<ResolvedReportTemplateDto> ResolveAsync(
        ReportTemplateTarget target, long? orgUnitId, long? userId,
        long? documentTypeId = null, long? mailCategoryId = null, CancellationToken ct = default)
    {
        // A print job normally names only the user. Fall back to that user's own department so a
        // department design applies without every caller having to look the department up.
        long? chosenTemplateId = null;
        if (userId is { } ownerId)
        {
            var owner = await db.Users.IgnoreQueryFilters()
                .Where(u => u.Id == ownerId)
                .Select(u => new { u.OrgUnitId, u.DefaultPrintTemplateId, u.DefaultMailTemplateId })
                .FirstOrDefaultAsync(ct);
            orgUnitId ??= owner?.OrgUnitId;
            // Each register carries its own choice: an internal message and an outgoing letter
            // are written on different letterheads by the same person.
            chosenTemplateId = target == ReportTemplateTarget.Correspondence
                ? owner?.DefaultMailTemplateId
                : owner?.DefaultPrintTemplateId;
        }

        // Candidates: active templates whose target matches the request or is 'All', and which are
        // either un-narrowed or narrowed to exactly the type/category being printed.
        var candidates = await db.ReportTemplates
            .Where(t => t.IsActive
                && (t.Target == target || t.Target == ReportTemplateTarget.All)
                && (t.DocumentTypeId == null || t.DocumentTypeId == documentTypeId)
                && (t.MailCategoryId == null || t.MailCategoryId == mailCategoryId))
            .ToListAsync(ct);

        ReportTemplate? chosen = null;
        string source = "None";

        /* An explicit choice outranks every positional rule.
         *
         * Resolution by position answers "what applies to someone in your seat", which is the
         * right default but the wrong answer once a person has actually picked a design. The
         * choice is honoured only if it is still among the candidates, so a design that was
         * deleted, deactivated or narrowed to another type quietly falls back instead of
         * printing something that no longer fits the job. */
        if (chosenTemplateId is { } pinned)
        {
            chosen = candidates.FirstOrDefault(t => t.Id == pinned);
            if (chosen is not null) source = "UserChoice";
        }

        if (chosen is null && userId is { } uid)
        {
            chosen = Best(candidates.Where(t => t.Scope == ReportTemplateScope.User && t.UserId == uid));
            if (chosen is not null) source = "User";
        }
        if (chosen is null && orgUnitId is { } oid)
        {
            chosen = Best(candidates.Where(t => t.Scope == ReportTemplateScope.OrgUnit && t.OrgUnitId == oid));
            if (chosen is not null) source = "OrgUnit";
        }
        if (chosen is null)
        {
            chosen = Best(candidates.Where(t => t.Scope == ReportTemplateScope.Default));
            if (chosen is not null) source = "Default";
        }

        // Logo falls back to the institution logo when the chosen design has none (or no design at all).
        var instLogo = await db.Institutions.OrderBy(i => i.Id)
            .Select(i => i.LogoBase64).FirstOrDefaultAsync(ct);

        return new ResolvedReportTemplateDto(
            chosen?.Id,
            chosen?.Name ?? "التصميم الافتراضي",
            chosen?.LogoBase64 ?? instLogo,
            chosen?.LayoutJson ?? "[]",
            source,
            IsNarrowed(chosen));

        // Within one scope level, the most specific design wins: one tied to this document type /
        // mail category, then one whose target is exact rather than 'All', then the newest.
        ReportTemplate? Best(IEnumerable<ReportTemplate> level) => level
            .OrderByDescending(IsNarrowed)
            .ThenByDescending(t => t.Target == target)
            .ThenByDescending(t => t.Id)
            .FirstOrDefault();

        bool IsNarrowed(ReportTemplate? t) => t is not null &&
            ((documentTypeId is not null && t.DocumentTypeId == documentTypeId)
             || (mailCategoryId is not null && t.MailCategoryId == mailCategoryId));
    }

    // ---- helpers ----

    private static string? Validate(SaveReportTemplateRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "اسم التصميم مطلوب";
        if (r.Scope == ReportTemplateScope.OrgUnit && r.OrgUnitId is null) return "اختر القسم لتصميم على مستوى القسم";
        if (r.Scope == ReportTemplateScope.User && r.UserId is null) return "اختر المستخدم لتصميم على مستوى المستخدم";
        if (r.Target == ReportTemplateTarget.All && (r.DocumentTypeId is not null || r.MailCategoryId is not null))
            return "لا يمكن تخصيص التصميم لنوع معيّن مع «يُطبَّق على: الكل» — اختر المراسلات أو الوثائق";
        return null;
    }

    // A design is narrowed by whichever dimension its target actually has: document types apply to
    // documents, mail categories to correspondence. The other is dropped rather than stored dead.
    private static long? NarrowedDocumentType(SaveReportTemplateRequest r) =>
        r.Target == ReportTemplateTarget.Document ? r.DocumentTypeId : null;

    private static long? NarrowedMailCategory(SaveReportTemplateRequest r) =>
        r.Target == ReportTemplateTarget.Correspondence ? r.MailCategoryId : null;

    private async Task<long?> DefaultInstitutionIdAsync(CancellationToken ct) =>
        await db.Institutions.OrderBy(i => i.Id).Select(i => (long?)i.Id).FirstOrDefaultAsync(ct);

    private async Task<List<ReportTemplateDto>> ToDtosAsync(IReadOnlyList<ReportTemplate> items, CancellationToken ct)
    {
        var unitIds = items.Where(t => t.OrgUnitId is not null).Select(t => t.OrgUnitId!.Value).Distinct().ToList();
        var userIds = items.Where(t => t.UserId is not null).Select(t => t.UserId!.Value).Distinct().ToList();
        var unitNames = await db.OrgUnits.Where(u => unitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var userNames = await db.Users.IgnoreQueryFilters().Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var typeIds = items.Where(t => t.DocumentTypeId is not null).Select(t => t.DocumentTypeId!.Value).Distinct().ToList();
        var catIds = items.Where(t => t.MailCategoryId is not null).Select(t => t.MailCategoryId!.Value).Distinct().ToList();
        var typeNames = await db.DocumentTypes.Where(x => typeIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var catNames = await db.MailCategories.Where(x => catIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        // Resolved once per response, not per row: the answer is the caller's, not the design's,
        // apart from the scope/owner comparison each row makes against it.
        var isAdmin = await scope.IsSystemAdminAsync(ct);
        IReadOnlyList<long> myDepts = isAdmin ? [] : await scope.OrgUnitIdsAsync(ct);
        var me = currentUser.UserId;
        bool CanEdit(ReportTemplate t) => isAdmin || t.Scope switch
        {
            ReportTemplateScope.User => t.UserId == me,
            ReportTemplateScope.OrgUnit => t.OrgUnitId is { } u && myDepts.Contains(u),
            _ => false,
        };

        return items.Select(t => new ReportTemplateDto(
            t.Id, t.InstitutionId, t.Scope.ToString(),
            t.OrgUnitId, t.OrgUnitId is { } oid && unitNames.TryGetValue(oid, out var un) ? un : null,
            t.UserId, t.UserId is { } uid && userNames.TryGetValue(uid, out var nm) ? nm : null,
            t.Target.ToString(),
            t.DocumentTypeId, t.DocumentTypeId is { } dt && typeNames.TryGetValue(dt, out var tn) ? tn : null,
            t.MailCategoryId, t.MailCategoryId is { } mc && catNames.TryGetValue(mc, out var cn) ? cn : null,
            t.Name, t.LogoBase64, t.LayoutJson, t.IsActive, CanEdit(t))).ToList();
    }
}
