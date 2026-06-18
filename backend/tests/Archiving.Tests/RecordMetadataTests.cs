using Archiving.Application.Common.Interfaces;
using Archiving.Domain.Entities;
using Archiving.Domain.Enums;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Archiving.Tests;

public class RecordMetadataTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Derives_creator_and_custodian_and_version_relationship()
    {
        using var db = NewDb();
        db.OrgUnits.Add(new OrgUnit { Id = 1, Name = "الإدارة العامة", InstitutionId = 1 });
        db.Users.Add(new User { Id = 5, FullName = "مدير النظام", Email = "admin@x" });
        db.Documents.Add(new Document { Id = 10, DocumentNumber = "DOC-9", Title = "v2", DocumentTypeId = 1, OwningOrgUnitId = 1, CreatedBy = 5, ParentDocumentId = 9 });
        db.Documents.Add(new Document { Id = 9, DocumentNumber = "DOC-8", Title = "v1", DocumentTypeId = 1, OwningOrgUnitId = 1 });
        await db.SaveChangesAsync();

        var svc = new RecordMetadataService(db, new TestCurrentUser(), new NoopAuditWriter());
        var result = await svc.GetForDocumentAsync(10);

        Assert.True(result.Succeeded);
        var m = result.Value!;
        Assert.Contains(m.Agents, a => a.Role == "Creator" && a.AgentName == "مدير النظام");
        Assert.Contains(m.Agents, a => a.Role == "Custodian" && a.AgentName == "الإدارة العامة");
        Assert.Contains(m.Relationships, r => r.Type == "IsVersionOf" && r.TargetId == 9);
    }

    [Fact]
    public async Task Derivation_is_idempotent()
    {
        using var db = NewDb();
        db.OrgUnits.Add(new OrgUnit { Id = 1, Name = "U", InstitutionId = 1 });
        db.Documents.Add(new Document { Id = 1, DocumentNumber = "DOC-1", Title = "T", DocumentTypeId = 1, OwningOrgUnitId = 1, CreatedBy = 5 });
        db.Users.Add(new User { Id = 5, FullName = "A", Email = "a@x" });
        await db.SaveChangesAsync();

        var svc = new RecordMetadataService(db, new TestCurrentUser(), new NoopAuditWriter());
        await svc.GetForDocumentAsync(1);
        await svc.GetForDocumentAsync(1);

        Assert.Equal(2, await db.RecordAgents.CountAsync()); // Creator + Custodian, not duplicated
    }
}
