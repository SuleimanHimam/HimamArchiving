using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Metadata;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>Maintains and reads ISO 23081 metadata for records: which agents act on them (with roles),
/// how they relate to other records, and which business activities (workflows) they took part in.</summary>
public sealed class RecordMetadataService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditWriter audit) : IRecordMetadataService
{
    private const string DocType = "Document";

    public async Task<Result<RecordMetadataDto>> GetForDocumentAsync(long documentId, CancellationToken ct = default)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (doc is null) return Result<RecordMetadataDto>.Fail("الوثيقة غير موجودة");
        if ((int)doc.Confidentiality > (int)currentUser.Clearance)
            return Result<RecordMetadataDto>.Fail("لا تملك صلاحية الوصول لهذه الوثيقة");

        await EnsureDerivedAsync(doc, ct);

        var agents = await db.RecordAgents.Where(a => a.RecordType == DocType && a.RecordId == documentId).ToListAsync(ct);
        var rels = await db.RecordRelationships.Where(r => r.SourceType == DocType && r.SourceId == documentId).ToListAsync(ct);

        var agentDtos = await ResolveAgentsAsync(agents, ct);
        var relDtos = await ResolveRelationshipsAsync(rels, ct);

        var activities = await db.WorkflowInstances
            .Where(i => i.EntityType == DocType && i.EntityId == documentId)
            .OrderByDescending(i => i.Id)
            .Select(i => new RecordActivityDto(i.Id, i.WorkflowDefinition.Name, i.Status.ToString(), i.StartedAt))
            .ToListAsync(ct);

        return Result<RecordMetadataDto>.Ok(new RecordMetadataDto(
            doc.Id, doc.DocumentNumber, doc.Title, agentDtos, relDtos, activities));
    }

    public async Task<Result<RecordAgentDto>> AddAgentAsync(string recordType, long recordId, AddAgentRequest r, CancellationToken ct = default)
    {
        var added = await UpsertAgentAsync(recordType, recordId, r.AgentKind, r.AgentId, r.Role, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("MetadataAgentAdded", recordType, recordId, $"{r.Role}:{r.AgentKind}:{r.AgentId}", ct: ct);
        var dto = (await ResolveAgentsAsync([added], ct))[0];
        return Result<RecordAgentDto>.Ok(dto);
    }

    public async Task<Result<RecordRelationshipDto>> AddRelationshipAsync(AddRelationshipRequest r, CancellationToken ct = default)
    {
        var rel = await UpsertRelationshipAsync(r.SourceType, r.SourceId, r.TargetType, r.TargetId, r.Type, ct);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("MetadataRelationshipAdded", r.SourceType, r.SourceId, r.Type.ToString(), ct: ct);
        var dto = (await ResolveRelationshipsAsync([rel], ct))[0];
        return Result<RecordRelationshipDto>.Ok(dto);
    }

    // ---- derivation ----

    private async Task EnsureDerivedAsync(Document doc, CancellationToken ct)
    {
        if (doc.CreatedBy is { } creator)
            await UpsertAgentAsync(DocType, doc.Id, AgentKind.User, creator, AgentRole.Creator, ct);
        if (doc.OwnerPositionId is { } pos)
            await UpsertAgentAsync(DocType, doc.Id, AgentKind.Position, pos, AgentRole.Owner, ct);
        await UpsertAgentAsync(DocType, doc.Id, AgentKind.OrgUnit, doc.OwningOrgUnitId, AgentRole.Custodian, ct);

        if (doc.ParentDocumentId is { } parent)
            await UpsertRelationshipAsync(DocType, doc.Id, DocType, parent, RecordRelationshipType.IsVersionOf, ct);

        await db.SaveChangesAsync(ct);
    }

    private async Task<RecordAgent> UpsertAgentAsync(string recordType, long recordId, AgentKind kind, long agentId, AgentRole role, CancellationToken ct)
    {
        var existing = await db.RecordAgents.FirstOrDefaultAsync(a =>
            a.RecordType == recordType && a.RecordId == recordId && a.AgentKind == kind && a.AgentId == agentId && a.Role == role, ct);
        if (existing is not null) return existing;
        var added = new RecordAgent { RecordType = recordType, RecordId = recordId, AgentKind = kind, AgentId = agentId, Role = role };
        db.RecordAgents.Add(added);
        return added;
    }

    private async Task<RecordRelationship> UpsertRelationshipAsync(string st, long sid, string tt, long tid, RecordRelationshipType type, CancellationToken ct)
    {
        var existing = await db.RecordRelationships.FirstOrDefaultAsync(r =>
            r.SourceType == st && r.SourceId == sid && r.TargetType == tt && r.TargetId == tid && r.Type == type, ct);
        if (existing is not null) return existing;
        var added = new RecordRelationship { SourceType = st, SourceId = sid, TargetType = tt, TargetId = tid, Type = type };
        db.RecordRelationships.Add(added);
        return added;
    }

    // ---- name resolution ----

    private async Task<List<RecordAgentDto>> ResolveAgentsAsync(List<RecordAgent> agents, CancellationToken ct)
    {
        var userIds = agents.Where(a => a.AgentKind == AgentKind.User).Select(a => a.AgentId).Distinct().ToList();
        var posIds = agents.Where(a => a.AgentKind == AgentKind.Position).Select(a => a.AgentId).Distinct().ToList();
        var unitIds = agents.Where(a => a.AgentKind == AgentKind.OrgUnit).Select(a => a.AgentId).Distinct().ToList();

        var users = await db.Users.IgnoreQueryFilters().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        var positions = await db.Positions.Where(p => posIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Title, ct);
        var units = await db.OrgUnits.Where(o => unitIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Name, ct);

        string Name(AgentKind k, long id) => k switch
        {
            AgentKind.User => users.GetValueOrDefault(id, $"#{id}"),
            AgentKind.Position => positions.GetValueOrDefault(id, $"#{id}"),
            _ => units.GetValueOrDefault(id, $"#{id}"),
        };

        return agents.Select(a => new RecordAgentDto(a.Id, a.AgentKind.ToString(), a.AgentId, Name(a.AgentKind, a.AgentId), a.Role.ToString())).ToList();
    }

    private async Task<List<RecordRelationshipDto>> ResolveRelationshipsAsync(List<RecordRelationship> rels, CancellationToken ct)
    {
        var docTargetIds = rels.Where(r => r.TargetType == DocType).Select(r => r.TargetId).Distinct().ToList();
        var docTitles = await db.Documents.IgnoreQueryFilters().Where(d => docTargetIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => $"{d.DocumentNumber} — {d.Title}", ct);

        return rels.Select(r => new RecordRelationshipDto(
            r.Id, r.SourceType, r.SourceId, r.TargetType, r.TargetId, r.Type.ToString(),
            r.TargetType == DocType ? docTitles.GetValueOrDefault(r.TargetId) : null)).ToList();
    }
}
