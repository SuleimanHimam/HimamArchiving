using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Archive;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>Builds the archive browse tree and lists the documents under one of its nodes.
///
/// Both axes resolve to the SAME table of documents, so switching the tree never changes what a row
/// means — only which documents are in scope:
///   • Department — Document.OwningOrgUnitId walked over the OrgUnit hierarchy, then year, then type.
///   • Origin     — where the document came into the archive from. Both registers file into the same
///                  Documents table (DiwanMail.ArchivedDocumentId and InternalMail.ArchivedDocumentId),
///                  so both appear here: external parties by organisation and direction, internal
///                  messages by sending department. Documents linked to neither register are grouped
///                  under "مباشر" so nothing in the archive is invisible on this axis.
/// </summary>
public sealed class ArchiveTreeService(AppDbContext db, IAccessScope scope, ICurrentUser currentUser)
    : IArchiveTreeService
{
    // A document's archive year is its official date, falling back to when it was captured.
    private static int YearOf(Document d) => d.DocumentDate?.Year ?? d.CreatedAt.Year;

    /// <summary>
    /// The documents this caller may be told about — the same two gates <c>DocumentService.ListAsync</c>
    /// applies, because a tree is a summary of that list and the two have to agree.
    ///
    /// The tree was built over <c>db.Documents</c> wholesale, so holding Archive.View showed every
    /// department's folders, their counts, and the titles inside them. A count is not a harmless
    /// number: «الشؤون القانونية › 2026 › شكوى (7)» tells a clerk in another department precisely
    /// what was not theirs to know, and opening the node listed the titles. Clearance had the same
    /// hole — a سرّي document the caller cannot open was still counted in a folder they can.
    /// </summary>
    private async Task<IQueryable<Document>> VisibleAsync(CancellationToken ct)
    {
        var q = db.Documents.Where(d => (int)d.Confidentiality <= (int)currentUser.Clearance);
        if (await scope.SeesAllDocumentsAsync(ct)) return q;

        var me = currentUser.UserId;
        var depts = await scope.OrgUnitIdsAsync(ct);
        return q.Where(d => d.CreatedBy == me
            || depts.Contains(d.OwningOrgUnitId)
            || db.DocumentShares.Any(sh => sh.DocumentId == d.Id && sh.SharedWithUserId == me));
    }

    public async Task<IReadOnlyList<ArchiveTreeNodeDto>> GetTreeAsync(
        ArchiveTreeMode mode, CancellationToken ct = default) =>
        mode == ArchiveTreeMode.External
            ? await BuildExternalTreeAsync(ct)
            : await BuildDepartmentTreeAsync(ct);

    // ── Department axis ─────────────────────────────────────────────────────────────────────────
    private async Task<IReadOnlyList<ArchiveTreeNodeDto>> BuildDepartmentTreeAsync(CancellationToken ct)
    {
        var units = await db.OrgUnits
            .OrderBy(u => u.SortOrder).ThenBy(u => u.Name)
            .Select(u => new { u.Id, u.ParentId, u.Name, u.Code })
            .ToListAsync(ct);

        // One grouped pass gives every (department, year, type) bucket; the tree is assembled in memory.
        var buckets = await (await VisibleAsync(ct))
            .GroupBy(d => new
            {
                d.OwningOrgUnitId,
                Year = d.DocumentDate != null ? d.DocumentDate.Value.Year : d.CreatedAt.Year,
                d.DocumentTypeId,
            })
            .Select(g => new { g.Key.OwningOrgUnitId, g.Key.Year, g.Key.DocumentTypeId, Count = g.Count() })
            .ToListAsync(ct);

        var typeNames = await db.DocumentTypes.Select(t => new { t.Id, t.Name }).ToDictionaryAsync(t => t.Id, t => t.Name, ct);
        // Roots are held separately: a Dictionary cannot key on the null ParentId of a top-level unit.
        var roots = units.Where(u => u.ParentId is null).ToList();
        var childUnits = units.Where(u => u.ParentId is not null)
            .GroupBy(u => u.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());
        var byUnit = buckets.GroupBy(b => b.OwningOrgUnitId).ToDictionary(g => g.Key, g => g.ToList());

        // Year → type folders for the documents owned by this unit directly (not its children).
        List<ArchiveTreeNodeDto> OwnFolders(long unitId)
        {
            if (!byUnit.TryGetValue(unitId, out var rows)) return [];
            return rows
                .GroupBy(r => r.Year)
                .OrderByDescending(g => g.Key)
                .Select(yearGroup => new ArchiveTreeNodeDto(
                    ArchiveNodeSelector.YearId(unitId, yearGroup.Key),
                    yearGroup.Key.ToString(),
                    "Year",
                    yearGroup.Sum(r => r.Count),
                    Children: yearGroup
                        .GroupBy(r => r.DocumentTypeId)
                        .OrderBy(g => typeNames.GetValueOrDefault(g.Key, ""))
                        .Select(typeGroup => new ArchiveTreeNodeDto(
                            ArchiveNodeSelector.TypeId(unitId, yearGroup.Key, typeGroup.Key),
                            typeNames.GetValueOrDefault(typeGroup.Key, $"#{typeGroup.Key}"),
                            "DocumentType",
                            typeGroup.Sum(r => r.Count)))
                        .ToList()))
                .ToList();
        }

        // Whether an empty branch is worth drawing. For a caller who sees the whole institution an
        // empty department really is empty, and the tree has always shown it — hiding it would turn
        // a permission fix into "the archive lost a department". For a scoped caller the same branch
        // is empty only from where they stand, and a folder holding (0) still announces the name and
        // the shape of work that was not theirs to read, so it is dropped instead.
        var seesAll = await scope.SeesAllDocumentsAsync(ct);

        // Recursive build: a department's children are its sub-departments AND its own year folders,
        // and its count rolls the whole subtree up.
        ArchiveTreeNodeDto? BuildUnit(long id, string name, string? code)
        {
            var subUnits = childUnits.TryGetValue(id, out var kids)
                ? kids.Select(k => BuildUnit(k.Id, k.Name, k.Code)).OfType<ArchiveTreeNodeDto>().ToList()
                : [];
            var folders = OwnFolders(id);
            var own = folders.Sum(f => f.Count);
            var total = own + subUnits.Sum(s => s.Count);
            if (total == 0 && !seesAll) return null;

            return new ArchiveTreeNodeDto(
                ArchiveNodeSelector.DepartmentId_(id),
                name,
                "Department",
                total,
                code,
                [.. subUnits, .. folders]);
        }

        return roots.Select(r => BuildUnit(r.Id, r.Name, r.Code))
            .OfType<ArchiveTreeNodeDto>().ToList();
    }

    // ── External axis ───────────────────────────────────────────────────────────────────────────
    private async Task<IReadOnlyList<ArchiveTreeNodeDto>> BuildExternalTreeAsync(CancellationToken ct)
    {
        // Only register entries that actually produced an archived document participate — the tree
        // must count the same rows the table will show.
        var visibleDocs = await VisibleAsync(ct);
        var visibleIds = visibleDocs.Select(d => d.Id);

        var buckets = await db.DiwanMails
            .Where(m => m.ArchivedDocumentId != null && visibleIds.Contains(m.ArchivedDocumentId!.Value))
            .GroupBy(m => new { m.ExternalOrgId, m.Direction })
            .Select(g => new { g.Key.ExternalOrgId, g.Key.Direction, Count = g.Count() })
            .ToListAsync(ct);

        var orgNames = await db.ExternalOrganizations
            .Select(o => new { o.Id, o.Name })
            .ToDictionaryAsync(o => o.Id, o => o.Name, ct);

        var nodes = buckets
            .GroupBy(b => b.ExternalOrgId)
            .Select(orgGroup => new ArchiveTreeNodeDto(
                ArchiveNodeSelector.ExternalId(orgGroup.Key),
                orgGroup.Key is { } id ? orgNames.GetValueOrDefault(id, $"#{id}") : "غير محدد",
                "ExternalOrg",
                orgGroup.Sum(b => b.Count),
                Children: orgGroup
                    .OrderBy(b => b.Direction)
                    .Select(b => new ArchiveTreeNodeDto(
                        ArchiveNodeSelector.DirectionId(orgGroup.Key, b.Direction),
                        b.Direction == DiwanDirection.Incoming ? "وارد" : "صادر",
                        "Direction",
                        b.Count))
                    .ToList()))
            .OrderByDescending(n => n.Count)
            .ToList();

        // ── Internal register ───────────────────────────────────────────────────────────────────
        // Archived internal messages file into the same table. Before this they fell into "مباشر",
        // whose label claims the document never went through a register — true for a direct upload,
        // false and misleading for a message someone archived from المراسلات.
        var internalBuckets = await db.InternalMails
            .Where(m => m.ArchivedDocumentId != null && visibleIds.Contains(m.ArchivedDocumentId!.Value))
            .GroupBy(m => m.SenderOrgUnitId)
            .Select(g => new { SenderOrgUnitId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        if (internalBuckets.Count > 0)
        {
            var unitNames = await db.OrgUnits.Select(u => new { u.Id, u.Name })
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

            nodes.Add(new ArchiveTreeNodeDto(
                ArchiveNodeSelector.InternalAllId,
                "البريد الداخلي",
                "Internal",
                internalBuckets.Sum(b => b.Count),
                Children: internalBuckets
                    .Select(b => new ArchiveTreeNodeDto(
                        ArchiveNodeSelector.InternalUnitId(b.SenderOrgUnitId),
                        b.SenderOrgUnitId is { } uid ? unitNames.GetValueOrDefault(uid, $"#{uid}") : "غير محدد",
                        "Department",
                        b.Count))
                    .OrderByDescending(n => n.Count)
                    .ToList()));
        }

        // Everything else in the archive: filed directly, through neither register.
        var linkedIds = LinkedDocumentIds();
        var directCount = await visibleDocs.CountAsync(d => !linkedIds.Contains(d.Id), ct);
        if (directCount > 0)
            nodes.Add(new ArchiveTreeNodeDto(ArchiveNodeSelector.DirectId, "مباشر (بدون بريد)", "ExternalOrg", directCount));

        return nodes;
    }

    // ── Node-scoped document list ───────────────────────────────────────────────────────────────
    public async Task<PagedResult<ArchiveTreeDocumentDto>> GetNodeDocumentsAsync(
        string? nodeId, string? search, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var sel = ArchiveNodeSelector.Parse(nodeId);
        // The same predicate the tree counted with: a node's list can never hold more than its badge.
        var q = await VisibleAsync(ct);

        if (sel.DepartmentId is { } deptId)
        {
            // Subtree, not exact match: picking a directorate must include everything beneath it.
            var ids = await DescendantUnitIdsAsync(deptId, ct);
            q = q.Where(d => ids.Contains(d.OwningOrgUnitId));
        }
        if (sel.Year is { } year)
            q = q.Where(d => (d.DocumentDate != null ? d.DocumentDate.Value.Year : d.CreatedAt.Year) == year);
        if (sel.DocumentTypeId is { } typeId)
            q = q.Where(d => d.DocumentTypeId == typeId);

        if (sel.DirectOnly)
        {
            // "Direct" means neither register — a document filed from an internal message is not direct.
            var linked = LinkedDocumentIds();
            q = q.Where(d => !linked.Contains(d.Id));
        }
        else if (sel.InternalOnly)
        {
            var mail = db.InternalMails.Where(m => m.ArchivedDocumentId != null);
            if (sel.InternalOrgUnitId is { } unitId) mail = mail.Where(m => m.SenderOrgUnitId == unitId);
            var ids = mail.Select(m => m.ArchivedDocumentId!.Value);
            q = q.Where(d => ids.Contains(d.Id));
        }
        else if (sel.ExternalOrgId is not null || sel.ExternalNone)
        {
            var mail = db.DiwanMails.Where(m => m.ArchivedDocumentId != null);
            mail = sel.ExternalNone
                ? mail.Where(m => m.ExternalOrgId == null)
                : mail.Where(m => m.ExternalOrgId == sel.ExternalOrgId);
            if (sel.Direction is { } dir) mail = mail.Where(m => m.Direction == dir);

            var ids = mail.Select(m => m.ArchivedDocumentId!.Value);
            q = q.Where(d => ids.Contains(d.Id));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(d => d.Title.Contains(s) || d.DocumentNumber.Contains(s)
                          || (d.Keywords != null && d.Keywords.Contains(s)));
        }

        var total = await q.CountAsync(ct);
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 200) pageSize = 20;

        // Origin columns come from the register entry this document was filed from, if any.
        var items = await q
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new ArchiveTreeDocumentDto(
                d.Id,
                d.DocumentNumber,
                d.Title,
                db.DocumentTypes.Where(t => t.Id == d.DocumentTypeId).Select(t => t.Name).FirstOrDefault() ?? "",
                db.OrgUnits.Where(u => u.Id == d.OwningOrgUnitId).Select(u => u.Name).FirstOrDefault() ?? "",
                d.Confidentiality.ToString(),
                d.Status.ToString(),
                d.DocumentDate,
                d.CreatedAt,
                db.DiwanMails.Where(m => m.ArchivedDocumentId == d.Id)
                    .Select(m => m.ExternalOrgName ?? (m.ExternalOrgId != null
                        ? db.ExternalOrganizations.Where(o => o.Id == m.ExternalOrgId).Select(o => o.Name).FirstOrDefault()
                        : null))
                    .FirstOrDefault(),
                db.DiwanMails.Where(m => m.ArchivedDocumentId == d.Id)
                    .Select(m => m.Direction.ToString())
                    .FirstOrDefault(),
                db.DiwanMails.Any(m => m.ArchivedDocumentId == d.Id) ? "External"
                    : db.InternalMails.Any(m => m.ArchivedDocumentId == d.Id) ? "Internal"
                    : "Direct"))
            .ToListAsync(ct);

        return new PagedResult<ArchiveTreeDocumentDto>
        {
            Items = items, Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    /// <summary>Documents that entered the archive through either register. Both links are FK-less
    /// by design, so this is the one place the two are unioned.</summary>
    private IQueryable<long> LinkedDocumentIds() =>
        db.DiwanMails.Where(m => m.ArchivedDocumentId != null).Select(m => m.ArchivedDocumentId!.Value)
            .Concat(db.InternalMails.Where(m => m.ArchivedDocumentId != null).Select(m => m.ArchivedDocumentId!.Value));

    /// <summary>The unit plus every unit beneath it. Walks the loaded parent map rather than issuing
    /// a recursive CTE — the org hierarchy is small and already cached per request.</summary>
    private async Task<List<long>> DescendantUnitIdsAsync(long rootId, CancellationToken ct)
    {
        var pairs = await db.OrgUnits.Select(u => new { u.Id, u.ParentId }).ToListAsync(ct);
        var children = pairs.Where(p => p.ParentId != null)
            .GroupBy(p => p.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());

        List<long> acc = [rootId];
        var queue = new Queue<long>([rootId]);
        while (queue.Count > 0)
        {
            if (!children.TryGetValue(queue.Dequeue(), out var kids)) continue;
            foreach (var k in kids) { acc.Add(k); queue.Enqueue(k); }
        }
        return acc;
    }
}
