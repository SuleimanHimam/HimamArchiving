using System.Text.Json;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;
using Archiving.Domain.Core;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Core;

/// <summary>Shared helpers for the core services: input trimming and the row-version check.</summary>
internal static class CoreGuards
{
    public const string Stale = "تم تعديل السجل من مستخدم آخر. أعد تحميل الصفحة ثم حاول مجدداً";

    public static string Clean(string? value) => (value ?? string.Empty).Trim();

    public static string? CleanOrNull(string? value)
    {
        var v = Clean(value);
        return v.Length == 0 ? null : v;
    }

    /// <summary>
    /// False when the caller's copy of the row is older than the stored one — or when the caller sent
    /// no token at all. The token is mandatory on every update: making it optional would let a client
    /// skip the check simply by leaving the field out.
    /// </summary>
    public static bool IsCurrent(byte[] stored, string? supplied)
    {
        if (supplied is null) return false;
        try { return Convert.FromBase64String(supplied).AsSpan().SequenceEqual(stored); }
        catch (FormatException) { return false; }
    }

    public static string Json(object value) => JsonSerializer.Serialize(value);
}

public sealed class SiteService(AppDbContext db, IAuditWriter audit) : ISiteService
{
    private const string NotFound = "الموقع غير موجود";

    public async Task<IReadOnlyList<SiteDto>> ListAsync(CancellationToken ct = default) =>
        (await db.Sites.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<Result<SiteDto>> CreateAsync(SaveSiteRequest r, CancellationToken ct = default)
    {
        var (name, code) = (CoreGuards.Clean(r.Name), CoreGuards.Clean(r.Code));
        if (Validate(name, code) is { } error) return Result<SiteDto>.Fail(error);
        if (await db.Sites.IgnoreQueryFilters().AnyAsync(s => s.Code == code, ct)) return Result<SiteDto>.Fail("رمز الموقع مستخدم");

        var e = new Site { Name = name, NameEn = CoreGuards.CleanOrNull(r.NameEn), Code = code, IsActive = r.IsActive };
        db.Sites.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "Site", e.Id, e.Name, newValues: CoreGuards.Json(ToDto(e)), ct: ct);
        return Result<SiteDto>.Ok(ToDto(e));
    }

    public async Task<Result<SiteDto>> UpdateAsync(long id, SaveSiteRequest r, CancellationToken ct = default)
    {
        var e = await db.Sites.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (e is null) return Result<SiteDto>.Fail(NotFound);
        if (!CoreGuards.IsCurrent(e.RowVersion, r.RowVersion)) return Result<SiteDto>.Fail(CoreGuards.Stale);

        var (name, code) = (CoreGuards.Clean(r.Name), CoreGuards.Clean(r.Code));
        if (Validate(name, code) is { } error) return Result<SiteDto>.Fail(error);
        if (await db.Sites.IgnoreQueryFilters().AnyAsync(s => s.Code == code && s.Id != id, ct)) return Result<SiteDto>.Fail("رمز الموقع مستخدم");

        var before = CoreGuards.Json(ToDto(e));
        (e.Name, e.NameEn, e.Code, e.IsActive) = (name, CoreGuards.CleanOrNull(r.NameEn), code, r.IsActive);

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<SiteDto>.Fail(CoreGuards.Stale); }

        await audit.WriteAsync("Updated", "Site", e.Id, e.Name, before, CoreGuards.Json(ToDto(e)), ct);
        return Result<SiteDto>.Ok(ToDto(e));
    }

    public async Task<Result<bool>> DeleteAsync(long id, CancellationToken ct = default)
    {
        var e = await db.Sites.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (e is null) return Result<bool>.Fail(NotFound);
        if (await db.Employees.AnyAsync(x => x.SiteId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف موقع مرتبط بموظفين");
        if (await db.SiteLocations.AnyAsync(x => x.SiteId == id, ct))
            return Result<bool>.Fail("لا يمكن حذف موقع يحتوي مواقع فرعية");

        db.Sites.Remove(e);   // the interceptor turns this into a soft delete
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "Site", id, e.Name, ct: ct);
        return Result<bool>.Ok(true);
    }

    public async Task<IReadOnlyList<SiteLocationDto>> ListLocationsAsync(long siteId, CancellationToken ct = default) =>
        (await db.SiteLocations.AsNoTracking().Where(l => l.SiteId == siteId).OrderBy(l => l.Name).ToListAsync(ct))
            .Select(ToDto).ToList();

    public async Task<Result<SiteLocationDto>> CreateLocationAsync(long siteId, SaveSiteLocationRequest r, CancellationToken ct = default)
    {
        if (!await db.Sites.AnyAsync(s => s.Id == siteId, ct)) return Result<SiteLocationDto>.Fail(NotFound);

        var (name, code) = (CoreGuards.Clean(r.Name), CoreGuards.Clean(r.Code));
        if (Validate(name, code) is { } error) return Result<SiteLocationDto>.Fail(error);
        if (!Enum.TryParse<SiteLocationKind>(r.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            return Result<SiteLocationDto>.Fail("نوع الموقع غير صالح");
        if (await db.SiteLocations.IgnoreQueryFilters().AnyAsync(l => l.SiteId == siteId && l.Code == code, ct))
            return Result<SiteLocationDto>.Fail("رمز الموقع الفرعي مستخدم في هذا الموقع");

        var e = new SiteLocation
        {
            SiteId = siteId, Name = name, NameEn = CoreGuards.CleanOrNull(r.NameEn), Code = code, Kind = kind, IsActive = r.IsActive,
        };
        db.SiteLocations.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "SiteLocation", e.Id, e.Name, newValues: CoreGuards.Json(ToDto(e)), ct: ct);
        return Result<SiteLocationDto>.Ok(ToDto(e));
    }

    public async Task<Result<SiteLocationDto>> UpdateLocationAsync(long id, SaveSiteLocationRequest r, CancellationToken ct = default)
    {
        var e = await db.SiteLocations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (e is null) return Result<SiteLocationDto>.Fail("الموقع الفرعي غير موجود");
        if (!CoreGuards.IsCurrent(e.RowVersion, r.RowVersion)) return Result<SiteLocationDto>.Fail(CoreGuards.Stale);

        var (name, code) = (CoreGuards.Clean(r.Name), CoreGuards.Clean(r.Code));
        if (Validate(name, code) is { } error) return Result<SiteLocationDto>.Fail(error);
        if (!Enum.TryParse<SiteLocationKind>(r.Kind, ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
            return Result<SiteLocationDto>.Fail("نوع الموقع غير صالح");
        if (await db.SiteLocations.IgnoreQueryFilters().AnyAsync(l => l.SiteId == e.SiteId && l.Code == code && l.Id != id, ct))
            return Result<SiteLocationDto>.Fail("رمز الموقع الفرعي مستخدم في هذا الموقع");

        var before = CoreGuards.Json(ToDto(e));
        (e.Name, e.NameEn, e.Code, e.Kind, e.IsActive) = (name, CoreGuards.CleanOrNull(r.NameEn), code, kind, r.IsActive);

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<SiteLocationDto>.Fail(CoreGuards.Stale); }

        await audit.WriteAsync("Updated", "SiteLocation", e.Id, e.Name, before, CoreGuards.Json(ToDto(e)), ct);
        return Result<SiteLocationDto>.Ok(ToDto(e));
    }

    public async Task<Result<bool>> DeleteLocationAsync(long id, CancellationToken ct = default)
    {
        var e = await db.SiteLocations.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (e is null) return Result<bool>.Fail("الموقع الفرعي غير موجود");

        db.SiteLocations.Remove(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", "SiteLocation", id, e.Name, ct: ct);
        return Result<bool>.Ok(true);
    }

    private static string? Validate(string name, string code)
    {
        if (name.Length == 0) return "الاسم مطلوب";
        if (name.Length > 200) return "الاسم أطول من 200 حرف";
        if (code.Length == 0) return "الرمز مطلوب";
        if (code.Length > 32) return "الرمز أطول من 32 حرفاً";
        return null;
    }

    private static SiteDto ToDto(Site s) =>
        new(s.Id, s.Name, s.NameEn, s.Code, s.IsActive, Convert.ToBase64String(s.RowVersion));

    private static SiteLocationDto ToDto(SiteLocation l) =>
        new(l.Id, l.SiteId, l.Name, l.NameEn, l.Code, l.Kind.ToString(), l.IsActive, Convert.ToBase64String(l.RowVersion));
}
