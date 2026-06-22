using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Locations;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class LocationService(AppDbContext db, IAuditWriter audit) : ILocationService
{
    private static bool Full(Box b) => b.Capacity is { } cap && b.CurrentCount >= cap;

    // ===================== Buildings =====================
    public async Task<IReadOnlyList<BuildingDto>> ListBuildingsAsync(CancellationToken ct = default) =>
        await db.Buildings.OrderBy(x => x.NameAr)
            .Select(x => new BuildingDto(x.Id, x.NameAr, x.NameEn, x.Code, x.Address, x.Notes, x.IsActive,
                db.Rooms.Count(r => r.BuildingId == x.Id))).ToListAsync(ct);

    public async Task<Result<BuildingDto>> CreateBuildingAsync(BuildingRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.NameAr)) return Result<BuildingDto>.Fail("اسم المبنى مطلوب");
        if (!string.IsNullOrWhiteSpace(r.Code) && await db.Buildings.AnyAsync(b => b.Code == r.Code, ct))
            return Result<BuildingDto>.Fail("رمز المبنى مستخدم مسبقًا");
        var e = new Building { NameAr = r.NameAr.Trim(), NameEn = r.NameEn, Code = r.Code, Address = r.Address, Notes = r.Notes, IsActive = r.IsActive };
        db.Buildings.Add(e); await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "Building", e.Id, e.NameAr, ct: ct);
        return Result<BuildingDto>.Ok(new BuildingDto(e.Id, e.NameAr, e.NameEn, e.Code, e.Address, e.Notes, e.IsActive, 0));
    }

    public async Task<Result<BuildingDto>> UpdateBuildingAsync(long id, BuildingRequest r, CancellationToken ct = default)
    {
        var e = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (e is null) return Result<BuildingDto>.Fail("المبنى غير موجود");
        if (string.IsNullOrWhiteSpace(r.NameAr)) return Result<BuildingDto>.Fail("اسم المبنى مطلوب");
        if (!string.IsNullOrWhiteSpace(r.Code) && await db.Buildings.AnyAsync(b => b.Code == r.Code && b.Id != id, ct))
            return Result<BuildingDto>.Fail("رمز المبنى مستخدم مسبقًا");
        e.NameAr = r.NameAr.Trim(); e.NameEn = r.NameEn; e.Code = r.Code; e.Address = r.Address; e.Notes = r.Notes; e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", "Building", e.Id, e.NameAr, ct: ct);
        return Result<BuildingDto>.Ok(new BuildingDto(e.Id, e.NameAr, e.NameEn, e.Code, e.Address, e.Notes, e.IsActive,
            await db.Rooms.CountAsync(x => x.BuildingId == id, ct)));
    }

    public async Task<Result<bool>> DeleteBuildingAsync(long id, CancellationToken ct = default)
    {
        var e = await db.Buildings.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (e is null) return Result<bool>.Fail("المبنى غير موجود");
        if (await db.Rooms.AnyAsync(x => x.BuildingId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف مبنى يحتوي على غرف — احذف الغرف أولًا أو عطّل المبنى");
        db.Buildings.Remove(e); await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "Building", e.Id, e.NameAr, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ===================== Rooms =====================
    public async Task<IReadOnlyList<RoomDto>> ListRoomsAsync(long? buildingId, CancellationToken ct = default)
    {
        var q = db.Rooms.AsQueryable();
        if (buildingId is { } bid) q = q.Where(x => x.BuildingId == bid);
        return await q.OrderBy(x => x.NameAr).Select(x => new RoomDto(x.Id, x.BuildingId, x.Building.NameAr,
            x.NameAr, x.NameEn, x.RoomNumber, x.Floor, x.Notes, x.IsActive, db.Cabinets.Count(c => c.RoomId == x.Id))).ToListAsync(ct);
    }

    public async Task<Result<RoomDto>> CreateRoomAsync(RoomRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.NameAr)) return Result<RoomDto>.Fail("اسم الغرفة مطلوب");
        var bld = await db.Buildings.FirstOrDefaultAsync(b => b.Id == r.BuildingId, ct);
        if (bld is null) return Result<RoomDto>.Fail("المبنى غير موجود");
        var e = new Room { BuildingId = r.BuildingId, NameAr = r.NameAr.Trim(), NameEn = r.NameEn, RoomNumber = r.RoomNumber, Floor = r.Floor, Notes = r.Notes, IsActive = r.IsActive };
        db.Rooms.Add(e); await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "Room", e.Id, e.NameAr, ct: ct);
        return Result<RoomDto>.Ok(new RoomDto(e.Id, e.BuildingId, bld.NameAr, e.NameAr, e.NameEn, e.RoomNumber, e.Floor, e.Notes, e.IsActive, 0));
    }

    public async Task<Result<RoomDto>> UpdateRoomAsync(long id, RoomRequest r, CancellationToken ct = default)
    {
        var e = await db.Rooms.Include(x => x.Building).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<RoomDto>.Fail("الغرفة غير موجودة");
        if (string.IsNullOrWhiteSpace(r.NameAr)) return Result<RoomDto>.Fail("اسم الغرفة مطلوب");
        if (!await db.Buildings.AnyAsync(b => b.Id == r.BuildingId, ct)) return Result<RoomDto>.Fail("المبنى غير موجود");
        e.BuildingId = r.BuildingId; e.NameAr = r.NameAr.Trim(); e.NameEn = r.NameEn; e.RoomNumber = r.RoomNumber; e.Floor = r.Floor; e.Notes = r.Notes; e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", "Room", e.Id, e.NameAr, ct: ct);
        var bname = await db.Buildings.Where(b => b.Id == r.BuildingId).Select(b => b.NameAr).FirstAsync(ct);
        return Result<RoomDto>.Ok(new RoomDto(e.Id, e.BuildingId, bname, e.NameAr, e.NameEn, e.RoomNumber, e.Floor, e.Notes, e.IsActive,
            await db.Cabinets.CountAsync(c => c.RoomId == id, ct)));
    }

    public async Task<Result<bool>> DeleteRoomAsync(long id, CancellationToken ct = default)
    {
        var e = await db.Rooms.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<bool>.Fail("الغرفة غير موجودة");
        if (await db.Cabinets.AnyAsync(c => c.RoomId == id, ct) || await db.Boxes.AnyAsync(b => b.RoomId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف غرفة تحتوي على خزائن أو صناديق");
        // Drop this room's connections (both directions) before deleting.
        var conns = await db.RoomConnections.Where(c => c.RoomId == id || c.ConnectedRoomId == id).ToListAsync(ct);
        db.RoomConnections.RemoveRange(conns);
        db.Rooms.Remove(e); await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "Room", e.Id, e.NameAr, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ===================== Room connections =====================
    // Stored as one row per direction; Add/Remove mirror both so a link shows from either room.
    public async Task<IReadOnlyList<RoomConnectionDto>> ListConnectionsAsync(long roomId, CancellationToken ct = default) =>
        await db.RoomConnections.Where(c => c.RoomId == roomId)
            .Select(c => new RoomConnectionDto(c.Id, c.RoomId, c.ConnectedRoomId, c.ConnectedRoom.NameAr, c.ConnectionType, c.Notes))
            .ToListAsync(ct);

    public async Task<Result<RoomConnectionDto>> AddConnectionAsync(long roomId, RoomConnectionRequest r, CancellationToken ct = default)
    {
        if (roomId == r.ConnectedRoomId) return Result<RoomConnectionDto>.Fail("لا يمكن ربط الغرفة بنفسها");
        if (!await db.Rooms.AnyAsync(x => x.Id == roomId, ct)) return Result<RoomConnectionDto>.Fail("الغرفة غير موجودة");
        var other = await db.Rooms.FirstOrDefaultAsync(x => x.Id == r.ConnectedRoomId, ct);
        if (other is null) return Result<RoomConnectionDto>.Fail("الغرفة المتصلة غير موجودة");
        if (await db.RoomConnections.AnyAsync(c => c.RoomId == roomId && c.ConnectedRoomId == r.ConnectedRoomId, ct))
            return Result<RoomConnectionDto>.Fail("الربط موجود مسبقًا");

        var a = new RoomConnection { RoomId = roomId, ConnectedRoomId = r.ConnectedRoomId, ConnectionType = r.ConnectionType, Notes = r.Notes };
        db.RoomConnections.Add(a);
        if (!await db.RoomConnections.AnyAsync(c => c.RoomId == r.ConnectedRoomId && c.ConnectedRoomId == roomId, ct))
            db.RoomConnections.Add(new RoomConnection { RoomId = r.ConnectedRoomId, ConnectedRoomId = roomId, ConnectionType = r.ConnectionType, Notes = r.Notes });
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Connected", "Room", roomId, $"→ {other.NameAr}", ct: ct);
        return Result<RoomConnectionDto>.Ok(new RoomConnectionDto(a.Id, roomId, r.ConnectedRoomId, other.NameAr, r.ConnectionType, r.Notes));
    }

    public async Task<Result<bool>> RemoveConnectionAsync(long roomId, long connectionId, CancellationToken ct = default)
    {
        var c = await db.RoomConnections.FirstOrDefaultAsync(x => x.Id == connectionId && x.RoomId == roomId, ct);
        if (c is null) return Result<bool>.Fail("الربط غير موجود");
        var mirror = await db.RoomConnections.FirstOrDefaultAsync(x => x.RoomId == c.ConnectedRoomId && x.ConnectedRoomId == c.RoomId, ct);
        db.RoomConnections.Remove(c);
        if (mirror is not null) db.RoomConnections.Remove(mirror);
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    // ===================== Cabinets =====================
    public async Task<IReadOnlyList<CabinetDto>> ListCabinetsAsync(long? roomId, CancellationToken ct = default)
    {
        var q = db.Cabinets.AsQueryable();
        if (roomId is { } rid) q = q.Where(x => x.RoomId == rid);
        return await q.OrderBy(x => x.NameAr).Select(x => new CabinetDto(x.Id, x.RoomId, x.Room.NameAr, x.NameAr, x.NameEn,
            x.CabinetCode, x.ShelfCount, x.Notes, x.IsActive, db.Shelves.Count(s => s.CabinetId == x.Id))).ToListAsync(ct);
    }

    public async Task<Result<CabinetDto>> CreateCabinetAsync(CabinetRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.NameAr)) return Result<CabinetDto>.Fail("اسم الخزانة مطلوب");
        var room = await db.Rooms.FirstOrDefaultAsync(x => x.Id == r.RoomId, ct);
        if (room is null) return Result<CabinetDto>.Fail("الغرفة غير موجودة");
        var e = new Cabinet { RoomId = r.RoomId, NameAr = r.NameAr.Trim(), NameEn = r.NameEn, CabinetCode = r.CabinetCode, ShelfCount = r.ShelfCount, Notes = r.Notes, IsActive = r.IsActive };
        db.Cabinets.Add(e); await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "Cabinet", e.Id, e.NameAr, ct: ct);
        return Result<CabinetDto>.Ok(new CabinetDto(e.Id, e.RoomId, room.NameAr, e.NameAr, e.NameEn, e.CabinetCode, e.ShelfCount, e.Notes, e.IsActive, 0));
    }

    public async Task<Result<CabinetDto>> UpdateCabinetAsync(long id, CabinetRequest r, CancellationToken ct = default)
    {
        var e = await db.Cabinets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<CabinetDto>.Fail("الخزانة غير موجودة");
        if (string.IsNullOrWhiteSpace(r.NameAr)) return Result<CabinetDto>.Fail("اسم الخزانة مطلوب");
        if (!await db.Rooms.AnyAsync(x => x.Id == r.RoomId, ct)) return Result<CabinetDto>.Fail("الغرفة غير موجودة");
        e.RoomId = r.RoomId; e.NameAr = r.NameAr.Trim(); e.NameEn = r.NameEn; e.CabinetCode = r.CabinetCode; e.ShelfCount = r.ShelfCount; e.Notes = r.Notes; e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", "Cabinet", e.Id, e.NameAr, ct: ct);
        var rname = await db.Rooms.Where(x => x.Id == r.RoomId).Select(x => x.NameAr).FirstAsync(ct);
        return Result<CabinetDto>.Ok(new CabinetDto(e.Id, e.RoomId, rname, e.NameAr, e.NameEn, e.CabinetCode, e.ShelfCount, e.Notes, e.IsActive,
            await db.Shelves.CountAsync(s => s.CabinetId == id, ct)));
    }

    public async Task<Result<bool>> DeleteCabinetAsync(long id, CancellationToken ct = default)
    {
        var e = await db.Cabinets.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<bool>.Fail("الخزانة غير موجودة");
        if (await db.Shelves.AnyAsync(s => s.CabinetId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف خزانة تحتوي على رفوف");
        db.Cabinets.Remove(e); await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "Cabinet", e.Id, e.NameAr, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ===================== Shelves =====================
    public async Task<IReadOnlyList<ShelfDto>> ListShelvesAsync(long? cabinetId, CancellationToken ct = default)
    {
        var q = db.Shelves.AsQueryable();
        if (cabinetId is { } cid) q = q.Where(x => x.CabinetId == cid);
        return await q.OrderBy(x => x.ShelfNumber).Select(x => new ShelfDto(x.Id, x.CabinetId, x.Cabinet.NameAr,
            x.ShelfNumber, x.Capacity, x.Notes, x.IsActive, db.Boxes.Count(b => b.ShelfId == x.Id))).ToListAsync(ct);
    }

    public async Task<Result<ShelfDto>> CreateShelfAsync(ShelfRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.ShelfNumber)) return Result<ShelfDto>.Fail("رقم الرف مطلوب");
        var cab = await db.Cabinets.FirstOrDefaultAsync(x => x.Id == r.CabinetId, ct);
        if (cab is null) return Result<ShelfDto>.Fail("الخزانة غير موجودة");
        var e = new Shelf { CabinetId = r.CabinetId, ShelfNumber = r.ShelfNumber.Trim(), Capacity = r.Capacity, Notes = r.Notes, IsActive = r.IsActive };
        db.Shelves.Add(e); await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "Shelf", e.Id, e.ShelfNumber, ct: ct);
        return Result<ShelfDto>.Ok(new ShelfDto(e.Id, e.CabinetId, cab.NameAr, e.ShelfNumber, e.Capacity, e.Notes, e.IsActive, 0));
    }

    public async Task<Result<ShelfDto>> UpdateShelfAsync(long id, ShelfRequest r, CancellationToken ct = default)
    {
        var e = await db.Shelves.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<ShelfDto>.Fail("الرف غير موجود");
        if (string.IsNullOrWhiteSpace(r.ShelfNumber)) return Result<ShelfDto>.Fail("رقم الرف مطلوب");
        if (!await db.Cabinets.AnyAsync(x => x.Id == r.CabinetId, ct)) return Result<ShelfDto>.Fail("الخزانة غير موجودة");
        e.CabinetId = r.CabinetId; e.ShelfNumber = r.ShelfNumber.Trim(); e.Capacity = r.Capacity; e.Notes = r.Notes; e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", "Shelf", e.Id, e.ShelfNumber, ct: ct);
        var cname = await db.Cabinets.Where(x => x.Id == r.CabinetId).Select(x => x.NameAr).FirstAsync(ct);
        return Result<ShelfDto>.Ok(new ShelfDto(e.Id, e.CabinetId, cname, e.ShelfNumber, e.Capacity, e.Notes, e.IsActive,
            await db.Boxes.CountAsync(b => b.ShelfId == id, ct)));
    }

    public async Task<Result<bool>> DeleteShelfAsync(long id, CancellationToken ct = default)
    {
        var e = await db.Shelves.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<bool>.Fail("الرف غير موجود");
        if (await db.Boxes.AnyAsync(b => b.ShelfId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف رف يحتوي على صناديق");
        db.Shelves.Remove(e); await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "Shelf", e.Id, e.ShelfNumber, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ===================== Boxes =====================
    public async Task<IReadOnlyList<BoxDto>> ListBoxesAsync(long? shelfId, long? roomId, CancellationToken ct = default)
    {
        var q = db.Boxes.AsQueryable();
        if (shelfId is { } sid) q = q.Where(x => x.ShelfId == sid);
        if (roomId is { } rid) q = q.Where(x => x.RoomId == rid);
        var rows = await q.OrderBy(x => x.BoxCode).ToListAsync(ct);
        return rows.Select(x => new BoxDto(x.Id, x.ShelfId, x.RoomId, x.BoxCode, x.Barcode, x.Capacity, x.CurrentCount, Full(x), x.Notes, x.IsActive)).ToList();
    }

    public async Task<Result<BoxDto>> CreateBoxAsync(BoxRequest r, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(r.BoxCode)) return Result<BoxDto>.Fail("رمز الصندوق مطلوب");
        if (r.ShelfId is null && r.RoomId is null) return Result<BoxDto>.Fail("يجب أن يكون الصندوق على رف أو داخل غرفة");
        if (await db.Boxes.AnyAsync(b => b.BoxCode == r.BoxCode, ct)) return Result<BoxDto>.Fail("رمز الصندوق مستخدم مسبقًا");
        if (r.ShelfId is { } sid && !await db.Shelves.AnyAsync(s => s.Id == sid, ct)) return Result<BoxDto>.Fail("الرف غير موجود");
        if (r.RoomId is { } rid && !await db.Rooms.AnyAsync(s => s.Id == rid, ct)) return Result<BoxDto>.Fail("الغرفة غير موجودة");

        var e = new Box
        {
            ShelfId = r.ShelfId, RoomId = r.ShelfId is null ? r.RoomId : null,
            BoxCode = r.BoxCode.Trim(), Barcode = r.Barcode, Capacity = r.Capacity, Notes = r.Notes, IsActive = r.IsActive,
        };
        db.Boxes.Add(e); await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "Box", e.Id, e.BoxCode, ct: ct);
        return Result<BoxDto>.Ok(new BoxDto(e.Id, e.ShelfId, e.RoomId, e.BoxCode, e.Barcode, e.Capacity, e.CurrentCount, Full(e), e.Notes, e.IsActive));
    }

    public async Task<Result<BoxDto>> UpdateBoxAsync(long id, BoxRequest r, CancellationToken ct = default)
    {
        var e = await db.Boxes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<BoxDto>.Fail("الصندوق غير موجود");
        if (string.IsNullOrWhiteSpace(r.BoxCode)) return Result<BoxDto>.Fail("رمز الصندوق مطلوب");
        if (r.ShelfId is null && r.RoomId is null) return Result<BoxDto>.Fail("يجب أن يكون الصندوق على رف أو داخل غرفة");
        if (await db.Boxes.AnyAsync(b => b.BoxCode == r.BoxCode && b.Id != id, ct)) return Result<BoxDto>.Fail("رمز الصندوق مستخدم مسبقًا");
        e.ShelfId = r.ShelfId; e.RoomId = r.ShelfId is null ? r.RoomId : null;
        e.BoxCode = r.BoxCode.Trim(); e.Barcode = r.Barcode; e.Capacity = r.Capacity; e.Notes = r.Notes; e.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", "Box", e.Id, e.BoxCode, ct: ct);
        return Result<BoxDto>.Ok(new BoxDto(e.Id, e.ShelfId, e.RoomId, e.BoxCode, e.Barcode, e.Capacity, e.CurrentCount, Full(e), e.Notes, e.IsActive));
    }

    public async Task<Result<bool>> DeleteBoxAsync(long id, CancellationToken ct = default)
    {
        var e = await db.Boxes.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Result<bool>.Fail("الصندوق غير موجود");
        if (await db.Documents.IgnoreQueryFilters().AnyAsync(d => d.BoxId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف صندوق يحتوي على وثائق — عطّله بدلًا من ذلك للحفاظ على السجل");
        db.Boxes.Remove(e); await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "Box", e.Id, e.BoxCode, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ===================== Tree + breadcrumb + label code =====================
    public async Task<IReadOnlyList<LocationTreeNode>> GetTreeAsync(CancellationToken ct = default)
    {
        var buildings = await db.Buildings.OrderBy(x => x.NameAr).ToListAsync(ct);
        var rooms = await db.Rooms.ToListAsync(ct);
        var cabinets = await db.Cabinets.ToListAsync(ct);
        var shelves = await db.Shelves.ToListAsync(ct);
        var boxes = await db.Boxes.ToListAsync(ct);

        LocationTreeNode BoxNode(Box x) => new(x.Id, "Box", x.BoxCode, x.Barcode, []);
        LocationTreeNode ShelfNode(Shelf x) => new(x.Id, "Shelf", x.ShelfNumber, null,
            boxes.Where(b => b.ShelfId == x.Id).Select(BoxNode).ToList());
        LocationTreeNode CabinetNode(Cabinet x) => new(x.Id, "Cabinet", x.NameAr, x.CabinetCode,
            shelves.Where(s => s.CabinetId == x.Id).Select(ShelfNode).ToList());
        LocationTreeNode RoomNode(Room x) => new(x.Id, "Room", x.NameAr, x.RoomNumber,
            cabinets.Where(c => c.RoomId == x.Id).Select(CabinetNode).ToList()
                .Concat(boxes.Where(b => b.RoomId == x.Id).Select(BoxNode)).ToList());
        LocationTreeNode BuildingNode(Building x) => new(x.Id, "Building", x.NameAr, x.Code,
            rooms.Where(r => r.BuildingId == x.Id).Select(RoomNode).ToList());

        return buildings.Select(BuildingNode).ToList();
    }

    public async Task<Result<BreadcrumbDto>> GetBreadcrumbAsync(long boxId, CancellationToken ct = default)
    {
        var box = await db.Boxes.FirstOrDefaultAsync(x => x.Id == boxId, ct);
        if (box is null) return Result<BreadcrumbDto>.Fail("الصندوق غير موجود");

        var parts = new List<string>();
        var code = new List<string>();
        Room? room = null;
        Cabinet? cabinet = null;
        Shelf? shelf = null;

        if (box.ShelfId is { } sid)
        {
            shelf = await db.Shelves.FirstOrDefaultAsync(s => s.Id == sid, ct);
            cabinet = shelf is null ? null : await db.Cabinets.FirstOrDefaultAsync(c => c.Id == shelf.CabinetId, ct);
            room = cabinet is null ? null : await db.Rooms.FirstOrDefaultAsync(r => r.Id == cabinet.RoomId, ct);
        }
        else if (box.RoomId is { } rid)
        {
            room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == rid, ct);
        }
        var building = room is null ? null : await db.Buildings.FirstOrDefaultAsync(b => b.Id == room.BuildingId, ct);

        static string Clean(string s) => new(s.Where(char.IsLetterOrDigit).ToArray());
        if (building is not null) { parts.Add(building.NameAr); code.Add(string.IsNullOrWhiteSpace(building.Code) ? $"B{building.Id}" : Clean(building.Code)); }
        if (room is not null) { parts.Add(room.NameAr); code.Add(string.IsNullOrWhiteSpace(room.RoomNumber) ? $"R{room.Id}" : Clean(room.RoomNumber)); }
        if (cabinet is not null) { parts.Add(cabinet.NameAr); code.Add(string.IsNullOrWhiteSpace(cabinet.CabinetCode) ? $"C{cabinet.Id}" : Clean(cabinet.CabinetCode)); }
        if (shelf is not null) { parts.Add($"رف {shelf.ShelfNumber}"); code.Add($"S{Clean(shelf.ShelfNumber)}"); }
        parts.Add($"صندوق {box.BoxCode}"); code.Add($"BX{box.Id}");

        return Result<BreadcrumbDto>.Ok(new BreadcrumbDto(box.Id, string.Join(" / ", parts), string.Join("-", code), parts));
    }

    public async Task<Result<LocationAncestryDto>> GetBoxAncestryAsync(long boxId, CancellationToken ct = default)
    {
        var box = await db.Boxes.FirstOrDefaultAsync(x => x.Id == boxId, ct);
        if (box is null) return Result<LocationAncestryDto>.Fail("الصندوق غير موجود");

        long? shelfId = box.ShelfId, cabinetId = null, roomId = box.RoomId, buildingId = null;
        if (box.ShelfId is { } sid)
        {
            var shelf = await db.Shelves.FirstOrDefaultAsync(s => s.Id == sid, ct);
            cabinetId = shelf?.CabinetId;
            if (cabinetId is { } cid)
            {
                var cabinet = await db.Cabinets.FirstOrDefaultAsync(c => c.Id == cid, ct);
                roomId = cabinet?.RoomId;
            }
        }
        if (roomId is { } rid)
            buildingId = await db.Rooms.Where(r => r.Id == rid).Select(r => (long?)r.BuildingId).FirstOrDefaultAsync(ct);

        return Result<LocationAncestryDto>.Ok(new LocationAncestryDto(box.Id, shelfId, cabinetId, roomId, buildingId));
    }

    // ===================== Legacy data migration (best-effort, idempotent) =====================
    public async Task<Result<string>> MigrateLegacyAsync(CancellationToken ct = default)
    {
        var legacy = await db.PhysicalLocations.ToListAsync(ct);
        if (legacy.Count == 0) return Result<string>.Ok("لا توجد مواقع قديمة للترحيل");

        // Walk up the legacy parent chain to the nearest ancestor of a given type.
        PhysicalLocation? Up(PhysicalLocation l, PhysicalLocationType type)
        {
            var cur = (PhysicalLocation?)l;
            var seen = new HashSet<long>();
            while (cur is not null && seen.Add(cur.Id))
            {
                if (cur.Type == type) return cur;
                cur = cur.ParentId is { } p ? legacy.FirstOrDefault(x => x.Id == p) : null;
            }
            return null;
        }

        var newBuildings = await db.Buildings.ToListAsync(ct);
        var newRooms = await db.Rooms.ToListAsync(ct);
        var newCabinets = await db.Cabinets.ToListAsync(ct);
        var newShelves = await db.Shelves.ToListAsync(ct);
        int created = 0, linked = 0;

        async Task<Building> BuildingForAsync(PhysicalLocation l)
        {
            var src = Up(l, PhysicalLocationType.Building);
            var name = src?.Name ?? "مبنى مُرحّل";
            var ex = newBuildings.FirstOrDefault(b => b.NameAr == name);
            if (ex is not null) return ex;
            ex = new Building { NameAr = name, Code = src?.Code };
            db.Buildings.Add(ex); await db.SaveChangesAsync(ct); newBuildings.Add(ex); created++;
            return ex;
        }
        async Task<Room> RoomForAsync(PhysicalLocation l)
        {
            var src = Up(l, PhysicalLocationType.Room);
            var bld = await BuildingForAsync(l);
            var name = src?.Name ?? "غرفة مُرحّلة";
            var ex = newRooms.FirstOrDefault(r => r.BuildingId == bld.Id && r.NameAr == name);
            if (ex is not null) return ex;
            ex = new Room { BuildingId = bld.Id, NameAr = name, RoomNumber = src?.Code };
            db.Rooms.Add(ex); await db.SaveChangesAsync(ct); newRooms.Add(ex); created++;
            return ex;
        }
        async Task<Cabinet> CabinetForAsync(PhysicalLocation l)
        {
            var src = Up(l, PhysicalLocationType.Cabinet);
            var room = await RoomForAsync(l);
            var name = src?.Name ?? "خزانة مُرحّلة";
            var ex = newCabinets.FirstOrDefault(c => c.RoomId == room.Id && c.NameAr == name);
            if (ex is not null) return ex;
            ex = new Cabinet { RoomId = room.Id, NameAr = name, CabinetCode = src?.Code };
            db.Cabinets.Add(ex); await db.SaveChangesAsync(ct); newCabinets.Add(ex); created++;
            return ex;
        }
        async Task<Shelf> ShelfForAsync(PhysicalLocation l)
        {
            var src = Up(l, PhysicalLocationType.Shelf);
            var cab = await CabinetForAsync(l);
            var num = src?.Code ?? src?.Name ?? "1";
            var ex = newShelves.FirstOrDefault(s => s.CabinetId == cab.Id && s.ShelfNumber == num);
            if (ex is not null) return ex;
            ex = new Shelf { CabinetId = cab.Id, ShelfNumber = num };
            db.Shelves.Add(ex); await db.SaveChangesAsync(ct); newShelves.Add(ex); created++;
            return ex;
        }

        // Map each legacy box (or each leaf location used by archive items) to a new Box.
        var boxMap = new Dictionary<long, long>();
        foreach (var loc in legacy.Where(x => x.Type == PhysicalLocationType.Box))
        {
            var boxCode = string.IsNullOrWhiteSpace(loc.Code) ? $"LEG-{loc.Id}" : loc.Code!;
            if (await db.Boxes.AnyAsync(b => b.BoxCode == boxCode, ct)) { boxMap[loc.Id] = (await db.Boxes.FirstAsync(b => b.BoxCode == boxCode, ct)).Id; continue; }
            var shelf = await ShelfForAsync(loc);
            var nb = new Box { ShelfId = shelf.Id, BoxCode = boxCode, Notes = loc.Name };
            db.Boxes.Add(nb); await db.SaveChangesAsync(ct); created++;
            boxMap[loc.Id] = nb.Id;
        }

        // Link documents that were filed under a legacy Box location to their new Box.
        var items = await db.PhysicalArchiveItems.Where(i => i.DocumentId != null).ToListAsync(ct);
        foreach (var it in items)
        {
            if (!boxMap.TryGetValue(it.PhysicalLocationId, out var newBoxId)) continue;
            var doc = await db.Documents.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.Id == it.DocumentId, ct);
            if (doc is null || doc.BoxId is not null) continue;
            doc.BoxId = newBoxId;
            await AdjustBoxCountAsync(null, newBoxId, ct);
            linked++;
        }
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Migrated", "Location", 0, $"created={created} linked={linked}", ct: ct);
        return Result<string>.Ok($"تم الترحيل: {created} عنصرًا جديدًا، وربط {linked} وثيقة بصناديقها");
    }

    private async Task AdjustBoxCountAsync(long? oldBoxId, long? newBoxId, CancellationToken ct)
    {
        if (newBoxId is { } nb) { var b = await db.Boxes.FirstOrDefaultAsync(x => x.Id == nb, ct); if (b is not null) { b.CurrentCount++; await db.SaveChangesAsync(ct); } }
        if (oldBoxId is { } ob) { var b = await db.Boxes.FirstOrDefaultAsync(x => x.Id == ob, ct); if (b is { CurrentCount: > 0 }) { b.CurrentCount--; await db.SaveChangesAsync(ct); } }
    }
}
