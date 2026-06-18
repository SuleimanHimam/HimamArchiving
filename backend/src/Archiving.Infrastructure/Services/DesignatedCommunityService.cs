using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Preservation;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

/// <summary>The archive's single Designated Community record (ISO 14721).</summary>
public sealed class DesignatedCommunityService(AppDbContext db, IAuditWriter audit) : IDesignatedCommunityService
{
    public async Task<DesignatedCommunityDto> GetAsync(CancellationToken ct = default)
    {
        var c = await db.DesignatedCommunities.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        return c is null
            ? new DesignatedCommunityDto(string.Empty, null, null)
            : new DesignatedCommunityDto(c.Name, c.Description, c.RenderingExpectations);
    }

    public async Task<DesignatedCommunityDto> UpdateAsync(DesignatedCommunityDto request, CancellationToken ct = default)
    {
        var c = await db.DesignatedCommunities.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (c is null) { c = new DesignatedCommunity(); db.DesignatedCommunities.Add(c); }
        c.Name = request.Name;
        c.Description = request.Description;
        c.RenderingExpectations = request.RenderingExpectations;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("DesignatedCommunityUpdated", "DesignatedCommunity", c.Id, c.Name, ct: ct);
        return new DesignatedCommunityDto(c.Name, c.Description, c.RenderingExpectations);
    }
}
