using Archiving.Application.Features.Documents;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Archiving.Tests;

/// <summary>
/// The hand-over between a department and قسم الأرشيف.
///
/// A department registers a record and sends it; the archive gives it a shelf or gives it back.
/// What is worth pinning down is the boundary that makes the module mean anything: filing is the
/// archive's alone, a document is only ever archived once it has somewhere physical to be, and a
/// returned document lands back in a state its owner can actually fix and re-send.
/// </summary>
public class ArchiveIntakeTests
{
    private const long DeptA = 10, ArchiveDept = 20;
    private const long Clerk = 100, Archivist = 200;
    private const long PaperTypeId = 1, DigitalTypeId = 2;

    private static AppDbContext NewDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        db.Institutions.Add(new Institution { Id = 1, Name = "مؤسسة الاختبار" });
        db.OrgUnits.Add(new OrgUnit { Id = DeptA, InstitutionId = 1, Name = "قسم أ" });
        db.OrgUnits.Add(new OrgUnit { Id = ArchiveDept, InstitutionId = 1, Name = "قسم الأرشيف" });

        // The custody right is what separates the two people in this story.
        db.Roles.Add(new Role { Id = 1, Name = "Employee" });
        db.Roles.Add(new Role { Id = 2, Name = "Archive Officer" });
        db.Permissions.Add(new Permission { Id = 1, Code = "Archive.Archive", Resource = "Archive", Action = PermissionAction.Archive });
        db.RolePermissions.Add(new RolePermission { RoleId = 2, PermissionId = 1 });

        db.Users.Add(new User { Id = Clerk, FullName = "موظف أ", Email = "c@x", OrgUnitId = DeptA });
        db.Users.Add(new User { Id = Archivist, FullName = "أمين الأرشيف", Email = "a@x", OrgUnitId = ArchiveDept });
        db.UserRoles.Add(new UserRole { UserId = Clerk, RoleId = 1 });
        db.UserRoles.Add(new UserRole { UserId = Archivist, RoleId = 2 });

        db.DocumentTypes.Add(new DocumentType { Id = PaperTypeId, Name = "عقد ورقي", RequiresPhysicalArchiving = true, RetentionMonths = 120 });
        db.DocumentTypes.Add(new DocumentType { Id = DigitalTypeId, Name = "مذكرة رقمية", RequiresPhysicalArchiving = false, RetentionMonths = 12 });

        // One shelf with two boxes: an empty one and one already at capacity.
        db.Buildings.Add(new Building { Id = 1, NameAr = "المبنى الرئيسي" });
        db.Rooms.Add(new Room { Id = 1, BuildingId = 1, NameAr = "غرفة ١" });
        db.Cabinets.Add(new Cabinet { Id = 1, RoomId = 1, NameAr = "خزانة ١" });
        db.Shelves.Add(new Shelf { Id = 1, CabinetId = 1, ShelfNumber = "1" });
        db.Boxes.Add(new Box { Id = 1, ShelfId = 1, BoxCode = "BX-1", Capacity = 10, CurrentCount = 0 });
        db.Boxes.Add(new Box { Id = 2, ShelfId = 1, BoxCode = "BX-FULL", Capacity = 1, CurrentCount = 1 });
        // A box kept straight in the room (3-level mode), and a second building that holds nothing —
        // between them they say whether "everything under this place" means what it claims.
        db.Boxes.Add(new Box { Id = 3, RoomId = 1, BoxCode = "BX-ROOM", Capacity = 10, CurrentCount = 0 });
        db.Buildings.Add(new Building { Id = 2, NameAr = "المبنى الثاني" });

        db.SaveChanges();
        return db;
    }

    private static DocumentService Docs(AppDbContext db, long asUser)
    {
        var me = new MutableCurrentUser { UserId = asUser };
        return new DocumentService(db, me, new NoopFileStorage(), new NoopAuditWriter(),
            new NoopWorkflowService(), new AccessScope(db, me));
    }

    private static long SeedDocument(AppDbContext db, long typeId = PaperTypeId, long createdBy = Clerk)
    {
        var doc = new Document
        {
            DocumentNumber = $"DOC-{Guid.NewGuid():N}"[..12],
            Title = "عقد التوريد",
            DocumentTypeId = typeId,
            OwningOrgUnitId = DeptA,
            Status = DocumentStatus.Active,
            CreatedBy = createdBy,
        };
        db.Documents.Add(doc);
        db.SaveChanges();
        return doc.Id;
    }

    // ── Sending ──────────────────────────────────────────────────────────────

    /// <summary>The hand-over works with no workflow definition configured. This is the regression
    /// that matters: intake used to refuse the submission outright ("سير عمل الأرشفة غير مُهيّأ")
    /// and the whole path was unusable wherever that definition was absent.</summary>
    [Fact]
    public async Task Sending_parks_the_document_in_the_queue_without_a_workflow_definition()
    {
        var db = NewDb();
        var id = SeedDocument(db);

        Assert.True((await Docs(db, Clerk).SendToArchiveAsync(id)).Succeeded);

        var doc = db.Documents.Single(d => d.Id == id);
        Assert.Equal(DocumentStatus.PendingArchive, doc.Status);
        Assert.NotNull(doc.SentToArchiveAtUtc);

        var queue = await Docs(db, Archivist).ListArchiveIntakeAsync();
        var waiting = Assert.Single(queue);
        Assert.Equal(id, waiting.Id);
        Assert.Equal("قسم أ", waiting.OwningOrgUnitName);
        Assert.Equal("موظف أ", waiting.SentByName);
    }

    [Fact]
    public async Task A_type_that_needs_no_physical_filing_is_not_sendable()
    {
        var db = NewDb();
        var id = SeedDocument(db, DigitalTypeId);

        var sent = await Docs(db, Clerk).SendToArchiveAsync(id);
        Assert.False(sent.Succeeded);
        Assert.Equal(DocumentStatus.Active, db.Documents.Single(d => d.Id == id).Status);
    }

    // ── Filing ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Filing_gives_the_document_a_box_and_archives_it()
    {
        var db = NewDb();
        var id = SeedDocument(db);
        await Docs(db, Clerk).SendToArchiveAsync(id);

        Assert.True((await Docs(db, Archivist).FileToArchiveAsync(id, new FileToArchiveRequest(1, "الرف العلوي"))).Succeeded);

        var doc = db.Documents.Single(d => d.Id == id);
        Assert.Equal(DocumentStatus.Archived, doc.Status);
        Assert.Equal(1, doc.BoxId);
        // The box's own count has to follow, or the register is wrong about how full the shelf is.
        Assert.Equal(1, db.Boxes.Single(b => b.Id == 1).CurrentCount);
        Assert.Empty(await Docs(db, Archivist).ListArchiveIntakeAsync());
        // The submitter is told where it went.
        Assert.Contains(db.Notifications, n => n.RecipientUserId == Clerk && n.Body!.Contains("BX-1"));
    }

    /// <summary>Filing is the custody right, not a document right. A clerk who could file would be
    /// deciding where the institution's paper lives from their own desk.</summary>
    [Fact]
    public async Task Only_a_holder_of_the_custody_right_may_file()
    {
        var db = NewDb();
        var id = SeedDocument(db);
        await Docs(db, Clerk).SendToArchiveAsync(id);

        Assert.False((await Docs(db, Clerk).FileToArchiveAsync(id, new FileToArchiveRequest(1, null))).Succeeded);
        Assert.False((await Docs(db, Clerk).ReturnFromArchiveAsync(id, new ReturnFromArchiveRequest("لا"))).Succeeded);
        Assert.Equal(DocumentStatus.PendingArchive, db.Documents.Single(d => d.Id == id).Status);
    }

    [Fact]
    public async Task A_full_box_is_refused_and_the_document_stays_in_the_queue()
    {
        var db = NewDb();
        var id = SeedDocument(db);
        await Docs(db, Clerk).SendToArchiveAsync(id);

        var filed = await Docs(db, Archivist).FileToArchiveAsync(id, new FileToArchiveRequest(2, null));
        Assert.False(filed.Succeeded);

        var doc = db.Documents.Single(d => d.Id == id);
        Assert.Equal(DocumentStatus.PendingArchive, doc.Status);
        Assert.Null(doc.BoxId);
        Assert.Single(await Docs(db, Archivist).ListArchiveIntakeAsync());
    }

    // ── Returning ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Returning_sends_it_back_with_the_reason_and_it_can_be_re_sent()
    {
        var db = NewDb();
        var id = SeedDocument(db);
        await Docs(db, Clerk).SendToArchiveAsync(id);

        Assert.False((await Docs(db, Archivist).ReturnFromArchiveAsync(id, new ReturnFromArchiveRequest("  "))).Succeeded);
        Assert.True((await Docs(db, Archivist).ReturnFromArchiveAsync(id, new ReturnFromArchiveRequest("المرفق ناقص"))).Succeeded);

        var doc = db.Documents.Single(d => d.Id == id);
        // Active, not a dead end: the point of returning it is that the owner can fix it.
        Assert.Equal(DocumentStatus.Active, doc.Status);
        Assert.Equal("المرفق ناقص", doc.ArchiveReturnReason);
        Assert.Empty(await Docs(db, Archivist).ListArchiveIntakeAsync());
        Assert.Contains(db.Notifications, n => n.RecipientUserId == Clerk && n.Body!.Contains("المرفق ناقص"));

        // Second attempt: it queues again, carrying what was wrong the first time.
        Assert.True((await Docs(db, Clerk).SendToArchiveAsync(id)).Succeeded);
        var again = Assert.Single(await Docs(db, Archivist).ListArchiveIntakeAsync());
        Assert.Equal("المرفق ناقص", again.PreviousReturnReason);
    }

    /// <summary>Deleting a document stops everything in flight about it.
    ///
    /// It did not: the instance stayed Running, and the escalation sweep — which asks whether a task
    /// is open, not whether its subject still exists — kept notifying people about a document that
    /// returned 404 when they followed the link.</summary>
    [Fact]
    public async Task Deleting_a_document_cancels_the_workflow_still_running_about_it()
    {
        var db = NewDb();
        var id = SeedDocument(db);
        await Docs(db, Clerk).SendToArchiveAsync(id);

        // The submission opens no instance here (NoopWorkflowService), so stand one up as the real
        // engine would, with an open task on it.
        var instance = new WorkflowInstance
        {
            WorkflowDefinitionId = 1, EntityType = "Document", EntityId = id,
            Status = WorkflowStatus.Running,
        };
        db.WorkflowInstances.Add(instance);
        db.SaveChanges();
        db.WorkflowTasks.Add(new WorkflowTask
        {
            WorkflowInstanceId = instance.Id, StageId = 1,
            Status = WorkflowTaskStatus.Pending, DueAt = DateTime.UtcNow.AddDays(-1),
        });
        db.SaveChanges();

        Assert.True((await Docs(db, Archivist).DeleteAsync(id)).Succeeded);

        Assert.Equal(WorkflowStatus.Cancelled, db.WorkflowInstances.Single(w => w.Id == instance.Id).Status);
        Assert.All(db.WorkflowTasks.Where(t => t.WorkflowInstanceId == instance.Id),
            t => Assert.Equal(WorkflowTaskStatus.Completed, t.Status));
    }

    // ── Browsing by place ────────────────────────────────────────────────────

    /// <summary>Standing on a place and asking what is in it. A document records only the box it
    /// sits in, so every level above the box has to be resolved down to boxes first; the point of
    /// the test is that a building answers for its whole contents rather than for nothing.</summary>
    [Fact]
    public async Task A_place_lists_everything_filed_underneath_it_at_any_level()
    {
        var db = NewDb();
        var onShelf = SeedDocument(db);
        await Docs(db, Clerk).SendToArchiveAsync(onShelf);
        await Docs(db, Archivist).FileToArchiveAsync(onShelf, new FileToArchiveRequest(1, null));

        // Filed straight into the room's own box — the branch that skips shelf and cabinet entirely.
        var inRoom = SeedDocument(db);
        db.Documents.Single(d => d.Id == inRoom).BoxId = 3;
        db.SaveChanges();

        async Task<long[]> At(string kind, long id) =>
            [.. (await Docs(db, Archivist).ListAsync(new DocumentQuery(null, null, null, null,
                LocationKind: kind, LocationId: id, PageSize: 100))).Items.Select(i => i.Id)];

        Assert.Equal([onShelf], await At("Box", 1));
        Assert.Equal([onShelf], await At("Shelf", 1));
        Assert.Equal([onShelf], await At("Cabinet", 1));
        // The room holds both routes at once: the cabinet's shelf and its own box.
        Assert.Equal([onShelf, inRoom], (await At("Room", 1)).Order());
        Assert.Equal([onShelf, inRoom], (await At("Building", 1)).Order());

        // An empty building, and a level that does not exist, both answer honestly.
        Assert.Empty(await At("Building", 2));
        Assert.Empty(await At("Floor", 1));
    }

    /// <summary>An archived document is not in the queue, so neither action reaches it — otherwise
    /// "file" would be a way to move filed paper around without the move being recorded as a move.</summary>
    [Fact]
    public async Task Neither_action_touches_a_document_that_is_not_waiting()
    {
        var db = NewDb();
        var id = SeedDocument(db);
        await Docs(db, Clerk).SendToArchiveAsync(id);
        await Docs(db, Archivist).FileToArchiveAsync(id, new FileToArchiveRequest(1, null));

        Assert.False((await Docs(db, Archivist).FileToArchiveAsync(id, new FileToArchiveRequest(1, null))).Succeeded);
        Assert.False((await Docs(db, Archivist).ReturnFromArchiveAsync(id, new ReturnFromArchiveRequest("سبب"))).Succeeded);
        Assert.Equal(DocumentStatus.Archived, db.Documents.Single(d => d.Id == id).Status);
    }
}
