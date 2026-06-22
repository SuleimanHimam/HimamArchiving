using Archiving.Application.Features.Locations;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Archiving.Tests;

public class LocationTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static LocationService Svc(AppDbContext db) => new(db, new NoopAuditWriter());

    [Fact]
    public async Task Full_hierarchy_builds_breadcrumb_and_location_code()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var b = (await svc.CreateBuildingAsync(new BuildingRequest("Main", null, "B1", null, null))).Value!;
        var r = (await svc.CreateRoomAsync(new RoomRequest(b.Id, "Room 103", null, "R-103", null, null))).Value!;
        var c = (await svc.CreateCabinetAsync(new CabinetRequest(r.Id, "Cabinet 2", null, "C2", 0, null))).Value!;
        var s = (await svc.CreateShelfAsync(new ShelfRequest(c.Id, "4", null, null))).Value!;
        var box = (await svc.CreateBoxAsync(new BoxRequest(s.Id, null, "BX-12", null, 100, null))).Value!;

        var bc = (await svc.GetBreadcrumbAsync(box.Id)).Value!;
        Assert.Equal($"B1-R103-C2-S4-BX{box.Id}", bc.LocationCode);
        Assert.Contains("Main", bc.Path);
        Assert.Contains("Room 103", bc.Path);
    }

    [Fact]
    public async Task Delete_building_with_rooms_is_blocked()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var b = (await svc.CreateBuildingAsync(new BuildingRequest("Main", null, null, null, null))).Value!;
        await svc.CreateRoomAsync(new RoomRequest(b.Id, "R1", null, null, null, null));
        var del = await svc.DeleteBuildingAsync(b.Id);
        Assert.False(del.Succeeded);
    }

    [Fact]
    public async Task Room_cannot_connect_to_itself()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var b = (await svc.CreateBuildingAsync(new BuildingRequest("Main", null, null, null, null))).Value!;
        var r = (await svc.CreateRoomAsync(new RoomRequest(b.Id, "R1", null, null, null, null))).Value!;
        var res = await svc.AddConnectionAsync(r.Id, new RoomConnectionRequest(r.Id, "Door", null));
        Assert.False(res.Succeeded);
    }

    [Fact]
    public async Task Connection_is_mirrored_and_dedup()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var b = (await svc.CreateBuildingAsync(new BuildingRequest("Main", null, null, null, null))).Value!;
        var r1 = (await svc.CreateRoomAsync(new RoomRequest(b.Id, "R1", null, null, null, null))).Value!;
        var r2 = (await svc.CreateRoomAsync(new RoomRequest(b.Id, "R2", null, null, null, null))).Value!;
        Assert.True((await svc.AddConnectionAsync(r1.Id, new RoomConnectionRequest(r2.Id, "Door", null))).Succeeded);
        Assert.False((await svc.AddConnectionAsync(r1.Id, new RoomConnectionRequest(r2.Id, "Door", null))).Succeeded); // dup
        Assert.Single(await svc.ListConnectionsAsync(r2.Id)); // mirrored — visible from r2
    }

    [Fact]
    public async Task Box_reports_full_at_capacity()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var b = (await svc.CreateBuildingAsync(new BuildingRequest("Main", null, null, null, null))).Value!;
        var r = (await svc.CreateRoomAsync(new RoomRequest(b.Id, "R1", null, null, null, null))).Value!;
        var box = (await svc.CreateBoxAsync(new BoxRequest(null, r.Id, "BX-1", null, 2, null))).Value!;
        db.Boxes.First(x => x.Id == box.Id).CurrentCount = 2;
        db.SaveChanges();
        var fetched = (await svc.ListBoxesAsync(null, r.Id)).First();
        Assert.True(fetched.IsFull);
    }

    [Fact]
    public async Task Delete_box_with_documents_is_blocked()
    {
        using var db = NewDb();
        var svc = Svc(db);
        var b = (await svc.CreateBuildingAsync(new BuildingRequest("Main", null, null, null, null))).Value!;
        var r = (await svc.CreateRoomAsync(new RoomRequest(b.Id, "R1", null, null, null, null))).Value!;
        var box = (await svc.CreateBoxAsync(new BoxRequest(null, r.Id, "BX-1", null, null, null))).Value!;
        db.Documents.Add(new Document { DocumentNumber = "D1", Title = "t", DocumentTypeId = 1, OwningOrgUnitId = 1, BoxId = box.Id });
        db.SaveChanges();
        var del = await svc.DeleteBoxAsync(box.Id);
        Assert.False(del.Succeeded);
    }
}
