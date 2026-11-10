using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Physical;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class PhysicalArchiveService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit) : IPhysicalArchiveService
{
    public async Task<IReadOnlyList<PhysicalLocationDto>> ListLocationsAsync(CancellationToken ct = default) =>
        await db.PhysicalLocations.OrderBy(l => l.Type).ThenBy(l => l.Name)
            .Select(l => new PhysicalLocationDto(l.Id, l.ParentId, l.Name, l.Type.ToString(), l.Code, l.RfidTag, l.IsActive))
            .ToListAsync(ct);

    public async Task<Result<PhysicalLocationDto>> CreateLocationAsync(CreatePhysicalLocationRequest r, CancellationToken ct = default)
    {
        if (r.ParentId is { } pid && !await db.PhysicalLocations.AnyAsync(l => l.Id == pid, ct))
            return Result<PhysicalLocationDto>.Fail("الموقع الأب غير موجود");

        var e = new PhysicalLocation { ParentId = r.ParentId, Name = r.Name, Type = r.Type, Code = r.Code, RfidTag = r.RfidTag };
        db.PhysicalLocations.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "PhysicalLocation", e.Id, e.Name, ct: ct);
        return Result<PhysicalLocationDto>.Ok(new PhysicalLocationDto(e.Id, e.ParentId, e.Name, e.Type.ToString(), e.Code, e.RfidTag, e.IsActive));
    }

    public async Task<Result<PhysicalLocationDto>> UpdateLocationAsync(long id, UpdatePhysicalLocationRequest r, CancellationToken ct = default)
    {
        var e = await db.PhysicalLocations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (e is null) return Result<PhysicalLocationDto>.Fail("الموقع غير موجود");
        if (r.ParentId == id) return Result<PhysicalLocationDto>.Fail("لا يمكن جعل الموقع أبًا لنفسه");
        if (r.ParentId is { } pid && !await db.PhysicalLocations.AnyAsync(l => l.Id == pid, ct))
            return Result<PhysicalLocationDto>.Fail("الموقع الأب غير موجود");

        e.Name = r.Name; e.Type = r.Type; e.Code = r.Code; e.RfidTag = r.RfidTag; e.ParentId = r.ParentId; e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", "PhysicalLocation", e.Id, e.Name, ct: ct);
        return Result<PhysicalLocationDto>.Ok(new PhysicalLocationDto(e.Id, e.ParentId, e.Name, e.Type.ToString(), e.Code, e.RfidTag, e.IsActive));
    }

    public async Task<Result<bool>> DeleteLocationAsync(long id, CancellationToken ct = default)
    {
        var e = await db.PhysicalLocations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (e is null) return Result<bool>.Fail("الموقع غير موجود");
        if (await db.PhysicalLocations.AnyAsync(l => l.ParentId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف موقع يحتوي على مواقع فرعية");
        if (await db.PhysicalArchiveItems.AnyAsync(i => i.PhysicalLocationId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف موقع يحتوي على بنود مؤرشفة");

        db.PhysicalLocations.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "PhysicalLocation", e.Id, e.Name, ct: ct);
        return Result<bool>.Ok(true);
    }

    /// <summary>The archived-items register. Documents reach a physical place through two paths — the
    /// legacy PhysicalArchiveItems rows and the normalized hierarchy (Document.BoxId, which is what the
    /// Locations tree files into) — so both are listed here; otherwise anything filed into a box would
    /// never show up. Archived correspondence is covered too: archiving a letter writes a Document,
    /// and filing that document into a box surfaces it here.</summary>
    public async Task<IReadOnlyList<PhysicalArchiveItemDto>> ListItemsAsync(long? locationId, CancellationToken ct = default)
    {
        var q = db.PhysicalArchiveItems.Include(i => i.PhysicalLocation).AsQueryable();
        if (locationId is { } id) q = q.Where(i => i.PhysicalLocationId == id);
        var legacy = await q
            .Select(i => new PhysicalArchiveItemDto(
                i.Id, i.DocumentId, i.PhysicalLocationId, i.PhysicalLocation.Name,
                i.BoxNumber, i.FileNumber, i.ArchivedAt, i.Notes,
                db.Documents.Where(d => d.Id == i.DocumentId).Select(d => d.DocumentNumber).FirstOrDefault(),
                db.Documents.Where(d => d.Id == i.DocumentId).Select(d => d.Title).FirstOrDefault()))
            .ToListAsync(ct);

        // PhysicalLocation is a parented tree — building → room → cabinet → shelf → box — but the
        // item names only its leaf, so a legacy row read as a bare "BX-001" with no clue which room
        // it was in, and the place filter offered thirty-odd of them side by side at the top level
        // as if each were its own building. Give them the same "A › B › C" path the boxed rows
        // already carry and both kinds sit in one tree.
        var locPaths = await LocationPathsAsync(ct);
        legacy = [.. legacy.Select(i => locPaths.TryGetValue(i.PhysicalLocationId, out var p) && p.Length > 0
            ? i with { LocationName = p }
            : i)];

        // A legacy row already represents that document; don't list it twice.
        var covered = legacy.Where(i => i.DocumentId is not null).Select(i => i.DocumentId!.Value).ToHashSet();

        // Boxed documents carry no legacy PhysicalLocation, so they can't satisfy a legacy-location filter.
        var boxed = new List<PhysicalArchiveItemDto>();
        if (locationId is null)
        {
            var rows = await db.Documents
                .Where(d => d.BoxId != null && !d.IsTombstone && !covered.Contains(d.Id))
                .Select(d => new { d.Id, d.DocumentNumber, d.Title, d.BoxId, d.CreatedAt, d.UpdatedAt })
                .ToListAsync(ct);

            if (rows.Count > 0)
            {
                var boxIds = rows.Select(r => r.BoxId!.Value).Distinct().ToList();
                // One flat pass per level, joined in memory — cheaper and far more portable than
                // five nested correlated subqueries, and the tables are small.
                var paths = await (
                    from b in db.Boxes.Where(b => boxIds.Contains(b.Id))
                    join s in db.Shelves on b.ShelfId equals s.Id into sj
                    from s in sj.DefaultIfEmpty()
                    join c in db.Cabinets on s.CabinetId equals c.Id into cj
                    from c in cj.DefaultIfEmpty()
                    // A box hangs off a shelf, or sits directly in a room (3-level mode).
                    join rm in db.Rooms on (c != null ? c.RoomId : b.RoomId) equals rm.Id into rj
                    from rm in rj.DefaultIfEmpty()
                    join bl in db.Buildings on rm.BuildingId equals bl.Id into bj
                    from bl in bj.DefaultIfEmpty()
                    select new
                    {
                        b.Id, b.BoxCode,
                        Building = bl != null ? bl.NameAr : null,
                        Room = rm != null ? rm.NameAr : null,
                        Cabinet = c != null ? c.NameAr : null,
                        Shelf = s != null ? s.ShelfNumber : null,
                    }).ToListAsync(ct);

                var byBox = paths.ToDictionary(x => x.Id, x => new
                {
                    x.BoxCode,
                    Path = string.Join(" › ", new[] { x.Building, x.Room, x.Cabinet, x.Shelf, x.BoxCode }
                        .Where(s => !string.IsNullOrWhiteSpace(s))),
                });

                boxed.AddRange(rows.Select(r =>
                {
                    byBox.TryGetValue(r.BoxId!.Value, out var loc);
                    return new PhysicalArchiveItemDto(
                        0, r.Id, 0, loc?.Path ?? "", loc?.BoxCode, null,
                        r.UpdatedAt ?? r.CreatedAt, null, r.DocumentNumber, r.Title, "Box");
                }));
            }
        }

        return [.. legacy.Concat(boxed).OrderByDescending(i => i.ArchivedAt)];
    }

    /// <summary>Location id → the readable path to it ("المبنى الرئيسي › غرفة ١ › … › BX-001").
    /// The whole table is walked in memory: it holds one row per shelf and box in the building, which
    /// is small, and the alternative is a self-join per level for every item on the page.</summary>
    private async Task<Dictionary<long, string>> LocationPathsAsync(CancellationToken ct)
    {
        var all = await db.PhysicalLocations
            .Select(l => new { l.Id, l.ParentId, l.Name })
            .ToListAsync(ct);
        var byId = all.ToDictionary(l => l.Id);
        var cache = new Dictionary<long, string>();

        string PathOf(long id, int depth)
        {
            if (cache.TryGetValue(id, out var hit)) return hit;
            if (!byId.TryGetValue(id, out var node)) return string.Empty;
            // The depth cap is what keeps a corrupt parent chain (a cycle) from recursing forever.
            var parent = node.ParentId is { } pid && depth < 16 ? PathOf(pid, depth + 1) : string.Empty;
            var path = parent.Length > 0 ? $"{parent} › {node.Name}" : node.Name;
            cache[id] = path;
            return path;
        }

        foreach (var l in all) PathOf(l.Id, 0);
        return cache;
    }

    public async Task<Result<PhysicalArchiveItemDto>> CreateItemAsync(CreatePhysicalArchiveItemRequest r, CancellationToken ct = default)
    {
        if (r.DocumentId is null)
            return Result<PhysicalArchiveItemDto>.Fail("يجب ربط البند بوثيقة");

        var location = await db.PhysicalLocations.FirstOrDefaultAsync(l => l.Id == r.PhysicalLocationId, ct);
        if (location is null) return Result<PhysicalArchiveItemDto>.Fail("الموقع الفيزيائي غير موجود");

        if (r.DocumentId is { } docId && !await db.Documents.AnyAsync(d => d.Id == docId, ct))
            return Result<PhysicalArchiveItemDto>.Fail("الوثيقة غير موجودة");

        var e = new PhysicalArchiveItem
        {
            DocumentId = r.DocumentId,
            PhysicalLocationId = r.PhysicalLocationId,
            BoxNumber = r.BoxNumber,
            FileNumber = r.FileNumber,
            Notes = r.Notes,
            ArchivedAt = DateTime.UtcNow,
            ArchivedByUserId = currentUser.UserId,
        };
        db.PhysicalArchiveItems.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Archived", "PhysicalArchiveItem", e.Id, location.Name, ct: ct);

        return Result<PhysicalArchiveItemDto>.Ok(new PhysicalArchiveItemDto(
            e.Id, e.DocumentId, e.PhysicalLocationId, location.Name,
            e.BoxNumber, e.FileNumber, e.ArchivedAt, e.Notes));
    }

    public async Task<Result<PhysicalArchiveItemDto>> UpdateItemAsync(long id, UpdatePhysicalArchiveItemRequest r, CancellationToken ct = default)
    {
        var e = await db.PhysicalArchiveItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (e is null) return Result<PhysicalArchiveItemDto>.Fail("البند غير موجود");
        var location = await db.PhysicalLocations.FirstOrDefaultAsync(l => l.Id == r.PhysicalLocationId, ct);
        if (location is null) return Result<PhysicalArchiveItemDto>.Fail("الموقع الفيزيائي غير موجود");

        var moved = e.PhysicalLocationId != r.PhysicalLocationId;
        e.PhysicalLocationId = r.PhysicalLocationId;
        e.BoxNumber = r.BoxNumber; e.FileNumber = r.FileNumber; e.Notes = r.Notes;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync(moved ? "Moved" : "Edited", "PhysicalArchiveItem", e.Id, location.Name, ct: ct);

        return Result<PhysicalArchiveItemDto>.Ok(new PhysicalArchiveItemDto(
            e.Id, e.DocumentId, e.PhysicalLocationId, location.Name,
            e.BoxNumber, e.FileNumber, e.ArchivedAt, e.Notes,
            await db.Documents.Where(d => d.Id == e.DocumentId).Select(d => d.DocumentNumber).FirstOrDefaultAsync(ct),
            await db.Documents.Where(d => d.Id == e.DocumentId).Select(d => d.Title).FirstOrDefaultAsync(ct)));
    }

    public async Task<Result<bool>> DeleteItemAsync(long id, CancellationToken ct = default)
    {
        var e = await db.PhysicalArchiveItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (e is null) return Result<bool>.Fail("البند غير موجود");
        db.PhysicalArchiveItems.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Removed", "PhysicalArchiveItem", e.Id, e.BoxNumber ?? string.Empty, ct: ct);
        return Result<bool>.Ok(true);
    }
}
