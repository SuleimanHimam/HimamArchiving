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

    public async Task<IReadOnlyList<PhysicalArchiveItemDto>> ListItemsAsync(long? locationId, CancellationToken ct = default)
    {
        var q = db.PhysicalArchiveItems.Include(i => i.PhysicalLocation).AsQueryable();
        if (locationId is { } id) q = q.Where(i => i.PhysicalLocationId == id);
        return await q.OrderByDescending(i => i.ArchivedAt)
            .Select(i => new PhysicalArchiveItemDto(
                i.Id, i.DocumentId, i.IncomingMailId, i.PhysicalLocationId, i.PhysicalLocation.Name,
                i.BoxNumber, i.FileNumber, i.ArchivedAt, i.Notes,
                db.Documents.Where(d => d.Id == i.DocumentId).Select(d => d.DocumentNumber).FirstOrDefault(),
                db.Documents.Where(d => d.Id == i.DocumentId).Select(d => d.Title).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<Result<PhysicalArchiveItemDto>> CreateItemAsync(CreatePhysicalArchiveItemRequest r, CancellationToken ct = default)
    {
        if (r.DocumentId is null && r.IncomingMailId is null)
            return Result<PhysicalArchiveItemDto>.Fail("يجب ربط البند بوثيقة أو معاملة واردة");

        var location = await db.PhysicalLocations.FirstOrDefaultAsync(l => l.Id == r.PhysicalLocationId, ct);
        if (location is null) return Result<PhysicalArchiveItemDto>.Fail("الموقع الفيزيائي غير موجود");

        if (r.DocumentId is { } docId && !await db.Documents.AnyAsync(d => d.Id == docId, ct))
            return Result<PhysicalArchiveItemDto>.Fail("الوثيقة غير موجودة");
        if (r.IncomingMailId is { } mailId && !await db.IncomingMails.AnyAsync(m => m.Id == mailId, ct))
            return Result<PhysicalArchiveItemDto>.Fail("المعاملة الواردة غير موجودة");

        var e = new PhysicalArchiveItem
        {
            DocumentId = r.DocumentId,
            IncomingMailId = r.IncomingMailId,
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
            e.Id, e.DocumentId, e.IncomingMailId, e.PhysicalLocationId, location.Name,
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
            e.Id, e.DocumentId, e.IncomingMailId, e.PhysicalLocationId, location.Name,
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
