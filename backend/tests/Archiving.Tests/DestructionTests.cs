using System.Security.Cryptography;
using System.Text;
using Archiving.Application.Features.Destruction;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services;
using Archiving.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Archiving.Tests;

public class DestructionTests
{
    static DestructionTests() => QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Document AddDoc(AppDbContext db, DateOnly? expiry)
    {
        var d = new Document
        {
            DocumentNumber = "DOC-1", Title = "t", DocumentTypeId = 1, OwningOrgUnitId = 1,
            Status = DocumentStatus.Active, ExpiryDate = expiry,
        };
        db.Documents.Add(d);
        db.SaveChanges();
        return d;
    }

    private static readonly DateOnly Yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
    private static readonly DateOnly Tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

    private static DestructionService NewSvc(AppDbContext db, Archiving.Application.Common.Interfaces.ICurrentUser user) =>
        new(db, user, new NoopAuditWriter(), new DestructionEligibilityService(db),
            new NoopFileStorage(), new NoopCertificateService(), new FakePasswordHasher());

    [Fact]
    public async Task Eligible_when_retention_met_and_no_hold_no_workflow()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Yesterday);
        var r = await new DestructionEligibilityService(db).CheckAsync(doc.Id);
        Assert.True(r.Eligible);
        Assert.Empty(r.Reasons);
    }

    [Fact]
    public async Task Blocks_when_retention_not_met()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Tomorrow);
        var r = await new DestructionEligibilityService(db).CheckAsync(doc.Id);
        Assert.False(r.Eligible);
        Assert.Contains(r.Reasons, x => x.Contains("مدة الحفظ"));
    }

    [Fact]
    public async Task Blocks_when_under_active_legal_hold()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Yesterday);
        db.LegalHolds.Add(new LegalHold { Reason = "litigation", Scope = LegalHoldScope.Document, DocumentId = doc.Id, PlacedByUserId = 1 });
        db.SaveChanges();

        var r = await new DestructionEligibilityService(db).CheckAsync(doc.Id);
        Assert.False(r.Eligible);
        Assert.Contains(r.Reasons, x => x.Contains("حجز قانوني"));
    }

    [Fact]
    public async Task Released_legal_hold_does_not_block()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Yesterday);
        db.LegalHolds.Add(new LegalHold
        {
            Reason = "litigation", Scope = LegalHoldScope.Document, DocumentId = doc.Id,
            PlacedByUserId = 1, ReleasedAtUtc = DateTime.UtcNow, ReleasedByUserId = 1,
        });
        db.SaveChanges();

        var r = await new DestructionEligibilityService(db).CheckAsync(doc.Id);
        Assert.True(r.Eligible);
    }

    [Fact]
    public async Task Blocks_when_open_workflow()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Yesterday);
        db.WorkflowInstances.Add(new WorkflowInstance { EntityType = "Document", EntityId = doc.Id, Status = WorkflowStatus.Running });
        db.SaveChanges();

        var r = await new DestructionEligibilityService(db).CheckAsync(doc.Id);
        Assert.False(r.Eligible);
        Assert.Contains(r.Reasons, x => x.Contains("دورة عمل"));
    }

    [Fact]
    public async Task Two_person_rule_blocks_self_approval()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Yesterday);
        // TestCurrentUser is user 5 — they both create and try to approve.
        var svc = NewSvc(db, new TestCurrentUser());

        var created = await svc.CreateAsync(new CreateDestructionRequest(new[] { doc.Id }, "expired", 0, null, null));
        Assert.True(created.Succeeded);
        await svc.SubmitAsync(created.Value!.Id);

        var approve = await svc.ApproveAsync(created.Value!.Id, new DestructionDecisionRequest(null));
        Assert.False(approve.Succeeded);
        Assert.Contains("المستخدمَين", approve.Error);
    }

    [Fact]
    public async Task Cannot_request_destruction_of_held_document()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Yesterday);
        db.LegalHolds.Add(new LegalHold { Reason = "hold", Scope = LegalHoldScope.Document, DocumentId = doc.Id, PlacedByUserId = 1 });
        db.SaveChanges();
        var svc = NewSvc(db, new TestCurrentUser());

        var created = await svc.CreateAsync(new CreateDestructionRequest(new[] { doc.Id }, "expired", 0, null, null));
        Assert.False(created.Succeeded);
    }

    [Fact]
    public async Task Cancel_leaves_document_intact()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Yesterday);
        var svc = NewSvc(db, new TestCurrentUser());
        var created = await svc.CreateAsync(new CreateDestructionRequest(new[] { doc.Id }, "expired", 0, null, null));

        var cancelled = await svc.CancelAsync(created.Value!.Id);
        Assert.True(cancelled.Succeeded);
        Assert.Equal("Cancelled", cancelled.Value!.Status);

        var stillThere = await db.Documents.FirstAsync(d => d.Id == doc.Id);
        Assert.False(stillThere.IsTombstone);
    }

    [Fact]
    public async Task CryptoShred_renders_content_unrecoverable()
    {
        var root = Path.Combine(Path.GetTempPath(), "shred_" + Guid.NewGuid().ToString("N"));
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:RootPath"] = root, ["Storage:EncryptionKey"] = key,
        }).Build();
        var storage = new LocalFileStorage(config);

        var plaintext = Encoding.UTF8.GetBytes("top secret contract");
        var stored = await storage.SaveAsync("docs", "c.txt", new MemoryStream(plaintext));

        // Readable (decrypts to original) before destruction.
        await using (var s = await storage.OpenAsync(stored.StorageKey))
        {
            var ms = new MemoryStream();
            await s!.CopyToAsync(ms);
            Assert.Equal(plaintext, ms.ToArray());
        }

        var method = await storage.CryptoShredAsync(stored.StorageKey);
        Assert.Equal("CryptoShred", method);

        // Unrecoverable: the key material (and bytes) are gone.
        Assert.Null(await storage.OpenAsync(stored.StorageKey));

        Directory.Delete(root, recursive: true);
    }

    private static DestructionRequestDto SetupApproved(AppDbContext db, MutableCurrentUser user, DestructionService svc, long docId)
    {
        user.UserId = 5;
        var created = svc.CreateAsync(new CreateDestructionRequest(new[] { docId }, "expired", 0, null, null)).Result;
        svc.SubmitAsync(created.Value!.Id).Wait();
        user.UserId = 7;
        svc.ApproveAsync(created.Value!.Id, new DestructionDecisionRequest(null)).Wait();
        return created.Value!;
    }

    [Fact]
    public async Task Execute_tombstones_destroys_content_and_completes()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Yesterday);
        db.DocumentAttachments.Add(new DocumentAttachment
        { DocumentId = doc.Id, FileName = "f.pdf", ContentType = "application/pdf", FileExtension = "pdf", StorageKey = "k1", Checksum = "ABC123" });
        db.Users.Add(new User { Id = 9, FullName = "Exec", Email = "e@x", PasswordHash = "h:secret" });
        db.SaveChanges();

        var user = new MutableCurrentUser();
        var svc = NewSvc(db, user);
        var req = SetupApproved(db, user, svc, doc.Id);

        user.UserId = 9;   // executor != requester(5) != approver(7)
        var exec = await svc.ExecuteAsync(req.Id, new ExecuteDestructionRequest("secret", null, null));
        Assert.True(exec.Succeeded, exec.Error);
        Assert.Equal("Completed", exec.Value!.Status);

        var d = await db.Documents.IgnoreQueryFilters().FirstAsync(x => x.Id == doc.Id);
        Assert.True(d.IsTombstone);
        Assert.NotNull(d.DestroyedAtUtc);
        var att = await db.DocumentAttachments.FirstAsync(a => a.DocumentId == doc.Id);
        Assert.True(att.ContentDestroyed);
    }

    [Fact]
    public async Task Execute_requires_mfa_step_up()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Yesterday);
        db.Users.Add(new User { Id = 9, FullName = "Exec", Email = "e@x", PasswordHash = "h:secret" });
        db.SaveChanges();
        var user = new MutableCurrentUser();
        var svc = NewSvc(db, user);
        var req = SetupApproved(db, user, svc, doc.Id);

        user.UserId = 9;
        var bad = await svc.ExecuteAsync(req.Id, new ExecuteDestructionRequest("wrong-password", null, null));
        Assert.False(bad.Succeeded);
        Assert.Contains("التحقق الأمني", bad.Error);
    }

    [Fact]
    public async Task Execute_blocked_for_requester_two_person()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Yesterday);
        db.Users.Add(new User { Id = 5, FullName = "Req", Email = "r@x", PasswordHash = "h:secret" });
        db.SaveChanges();
        var user = new MutableCurrentUser();
        var svc = NewSvc(db, user);
        var req = SetupApproved(db, user, svc, doc.Id);

        user.UserId = 5;   // the requester tries to execute
        var r = await svc.ExecuteAsync(req.Id, new ExecuteDestructionRequest("secret", null, null));
        Assert.False(r.Succeeded);
        Assert.Contains("فصل المهام", r.Error);
    }

    [Fact]
    public async Task Override_lets_manager_destroy_own_request_directly()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Yesterday);
        db.Users.Add(new User { Id = 5, FullName = "Mgr", Email = "m@x", PasswordHash = "h:secret" });
        db.SaveChanges();
        var user = new MutableCurrentUser { UserId = 5 };
        var svc = NewSvc(db, user);
        var created = await svc.CreateAsync(new CreateDestructionRequest(new[] { doc.Id }, "expired", 0, null, null));
        await svc.SubmitAsync(created.Value!.Id);

        // The same manager (5) requested and now executes — allowed because canOverride waives two-person.
        var exec = await svc.ExecuteAsync(created.Value!.Id, new ExecuteDestructionRequest("secret", null, null), canOverride: true);
        Assert.True(exec.Succeeded, exec.Error);
        Assert.Equal("Completed", exec.Value!.Status);

        var d = await db.Documents.IgnoreQueryFilters().FirstAsync(x => x.Id == doc.Id);
        Assert.True(d.IsTombstone);
    }

    [Fact]
    public async Task Certificate_renders_pdfa_and_stores()
    {
        using var db = NewDb();
        var doc = AddDoc(db, Yesterday);
        db.Institutions.Add(new Institution { Name = "مؤسسة الاختبار" });
        db.Users.Add(new User { Id = 5, FullName = "Requester", Email = "r@x", PasswordHash = "h:x" });
        var req = new DestructionRequest
        {
            Reason = "expired", RequestedByUserId = 5, Status = DestructionStatus.Completed,
            ApprovedByUserId = 7, ExecutedByUserId = 9, ApprovedAtUtc = DateTime.UtcNow, ExecutedAtUtc = DateTime.UtcNow,
            Items = new List<DestructionItem> { new() { DocumentId = doc.Id, Method = DestructionMethod.CryptoShred, ChecksumBefore = "ABC123" } },
        };
        db.DestructionRequests.Add(req);
        db.SaveChanges();

        var root = Path.Combine(Path.GetTempPath(), "cert_" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:RootPath"] = root }).Build();
        var cert = new CertificateService(db, new LocalFileStorage(config));

        var certId = await cert.IssueAsync(req.Id);
        Assert.True(certId > 0);

        var opened = await cert.OpenAsync(req.Id);
        Assert.NotNull(opened);
        var ms = new MemoryStream();
        await opened!.Value.Stream.CopyToAsync(ms);
        await opened.Value.Stream.DisposeAsync();   // release the file handle before cleanup
        var bytes = ms.ToArray();
        Assert.True(bytes.Length > 500);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));

        Directory.Delete(root, recursive: true);
    }
}
