using Archiving.Application.Features.Preservation;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Archiving.Tests;

public class PreservationPolicyTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Returns_sensible_defaults_when_none_configured()
    {
        using var db = NewDb();
        var svc = new PreservationPolicyService(db, new NoopAuditWriter());

        var p = await svc.GetAsync();
        Assert.Equal("PDF/A-2B", p.TargetPdfAConformance);
        Assert.True(p.AutoNormalizeOnIngest);
        Assert.Equal("SHA-256", p.FixityAlgorithm);
        Assert.Equal(1, p.FixityCadenceDays);
    }

    [Fact]
    public async Task Update_persists_and_clamps_and_defaults()
    {
        using var db = NewDb();
        var svc = new PreservationPolicyService(db, new NoopAuditWriter());

        var saved = await svc.UpdateAsync(new PreservationPolicyDto(
            Name: "", Description: "d", TargetPdfAConformance: "", AutoNormalizeOnIngest: false,
            FixityAlgorithm: "", FixityCadenceDays: 0, AllowedPreservationFormats: "PDF/A"));

        Assert.Equal("سياسة الحفظ", saved.Name);          // empty -> default
        Assert.Equal("PDF/A-2B", saved.TargetPdfAConformance); // empty -> default
        Assert.Equal("SHA-256", saved.FixityAlgorithm);   // empty -> default
        Assert.Equal(1, saved.FixityCadenceDays);          // 0 -> clamped to 1
        Assert.False(saved.AutoNormalizeOnIngest);

        var reloaded = await svc.GetAsync();
        Assert.False(reloaded.AutoNormalizeOnIngest);      // persisted
    }
}
