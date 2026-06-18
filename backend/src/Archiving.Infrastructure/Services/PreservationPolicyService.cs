using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Preservation;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class PreservationPolicyService(AppDbContext db, IAuditWriter audit) : IPreservationPolicyService
{
    public async Task<PreservationPolicyDto> GetAsync(CancellationToken ct = default)
    {
        var p = await db.PreservationPolicies.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        return p is null ? Defaults : Map(p);
    }

    public async Task<PreservationPolicyDto> UpdateAsync(PreservationPolicyDto r, CancellationToken ct = default)
    {
        var p = await db.PreservationPolicies.OrderBy(x => x.Id).FirstOrDefaultAsync(ct);
        if (p is null) { p = new PreservationPolicy(); db.PreservationPolicies.Add(p); }

        p.Name = string.IsNullOrWhiteSpace(r.Name) ? "سياسة الحفظ" : r.Name;
        p.Description = r.Description;
        p.TargetPdfAConformance = string.IsNullOrWhiteSpace(r.TargetPdfAConformance) ? "PDF/A-2B" : r.TargetPdfAConformance;
        p.AutoNormalizeOnIngest = r.AutoNormalizeOnIngest;
        p.FixityAlgorithm = string.IsNullOrWhiteSpace(r.FixityAlgorithm) ? "SHA-256" : r.FixityAlgorithm;
        p.FixityCadenceDays = Math.Clamp(r.FixityCadenceDays, 1, 3650);
        p.AllowedPreservationFormats = r.AllowedPreservationFormats;

        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("PreservationPolicyUpdated", "PreservationPolicy", p.Id, p.Name, ct: ct);
        return Map(p);
    }

    private static readonly PreservationPolicyDto Defaults =
        new("سياسة الحفظ الافتراضية", null, "PDF/A-2B", true, "SHA-256", 1, "PDF/A, JPEG, PNG, TIFF");

    private static PreservationPolicyDto Map(PreservationPolicy p) => new(
        p.Name, p.Description, p.TargetPdfAConformance, p.AutoNormalizeOnIngest,
        p.FixityAlgorithm, p.FixityCadenceDays, p.AllowedPreservationFormats);
}
