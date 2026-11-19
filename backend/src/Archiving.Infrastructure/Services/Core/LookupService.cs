using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;
using Archiving.Domain.Core;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Core;

/// <summary>Admin-managed reference lists. Categories are fixed in code; their values are data.</summary>
public sealed class LookupService(AppDbContext db, IAuditWriter audit) : ILookupService
{
    private const string NotFound = "القيمة غير موجودة";

    public async Task<IReadOnlyList<LookupDto>> ListAsync(string? category, bool activeOnly, CancellationToken ct = default)
    {
        var q = db.Lookups.AsNoTracking();
        if (CoreGuards.CleanOrNull(category) is { } c) q = q.Where(l => l.Category == c);
        if (activeOnly) q = q.Where(l => l.IsActive);
        var rows = await q.OrderBy(l => l.Category).ThenBy(l => l.SortOrder).ThenBy(l => l.Name).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<LookupDto>> CreateAsync(SaveLookupRequest r, CancellationToken ct = default)
    {
        var (f, error) = await ValidateAsync(r, null, ct);
        if (f is null) return Result<LookupDto>.Fail(error!);

        var e = new Lookup { Category = f.Category };
        Apply(e, f);
        db.Lookups.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "Lookup", e.Id, $"{e.Category}:{e.Name}", ct: ct);
        return Result<LookupDto>.Ok(ToDto(e));
    }

    public async Task<Result<LookupDto>> UpdateAsync(long id, SaveLookupRequest r, CancellationToken ct = default)
    {
        var e = await db.Lookups.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (e is null) return Result<LookupDto>.Fail(NotFound);
        if (!CoreGuards.IsCurrent(e.RowVersion, r.RowVersion)) return Result<LookupDto>.Fail(CoreGuards.Stale);
        if (r.Category != e.Category) return Result<LookupDto>.Fail("لا يمكن نقل القيمة إلى قائمة أخرى");

        var (f, error) = await ValidateAsync(r, id, ct);
        if (f is null) return Result<LookupDto>.Fail(error!);

        var before = CoreGuards.Json(ToDto(e));
        Apply(e, f);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<LookupDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Updated", "Lookup", e.Id, $"{e.Category}:{e.Name}", before, CoreGuards.Json(ToDto(e)), ct);
        return Result<LookupDto>.Ok(ToDto(e));
    }

    /// <summary>Soft delete. Records that point at the value keep it; it disappears from new choices.</summary>
    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var e = await db.Lookups.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (e is null) return Result<bool>.Fail(NotFound);
        db.Lookups.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "Lookup", id, $"{e.Category}:{e.Name}", ct: ct);
        return Result<bool>.Ok(true);
    }

    /// <summary>True when <paramref name="id"/> is an active value of <paramref name="category"/>.</summary>
    internal static Task<bool> IsValidAsync(AppDbContext db, long id, string category, CancellationToken ct) =>
        db.Lookups.AnyAsync(l => l.Id == id && l.Category == category && l.IsActive, ct);

    private sealed record Fields(string Category, string Code, string Name, string? NameEn, int SortOrder, bool IsActive);

    private static void Apply(Lookup e, Fields f)
    {
        e.Code = f.Code;
        e.Name = f.Name;
        e.NameEn = f.NameEn;
        e.SortOrder = f.SortOrder;
        e.IsActive = f.IsActive;
    }

    private async Task<(Fields?, string?)> ValidateAsync(SaveLookupRequest r, long? existingId, CancellationToken ct)
    {
        var category = CoreGuards.Clean(r.Category);
        var code = CoreGuards.Clean(r.Code);
        var name = CoreGuards.Clean(r.Name);
        if (!LookupCategories.All.Contains(category)) return (null, "القائمة غير معروفة");
        if (code.Length is 0 or > 64) return (null, "الرمز مطلوب ولا يتجاوز 64 حرفاً");
        if (name.Length is 0 or > 200) return (null, "الاسم مطلوب ولا يتجاوز 200 حرف");
        if (CoreGuards.Clean(r.NameEn).Length > 200) return (null, "الاسم بالإنجليزية أطول من 200 حرف");
        if (await db.Lookups.IgnoreQueryFilters()
                .AnyAsync(l => l.Category == category && l.Code == code && l.Id != existingId, ct))
            return (null, "الرمز مستخدم في هذه القائمة");
        return (new Fields(category, code, name, CoreGuards.CleanOrNull(r.NameEn), r.SortOrder, r.IsActive), null);
    }

    private static LookupDto ToDto(Lookup l) => new(l.Id, l.Category, l.Code, l.Name, l.NameEn, l.SortOrder,
        l.IsActive, Convert.ToBase64String(l.RowVersion));
}
