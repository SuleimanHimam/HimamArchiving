using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.External;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services;

public sealed class ExternalDirectoryService(AppDbContext db, IAuditWriter audit) : IExternalDirectoryService
{
    // ── Organizations ──────────────────────────────────────────────────────────
    public async Task<PagedResult<ExternalOrgDto>> ListOrgsAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var q = db.ExternalOrganizations.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(o => o.Name.Contains(s) || (o.NameEn != null && o.NameEn.Contains(s)) || (o.Code != null && o.Code.Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(o => o.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new ExternalOrgDto(o.Id, o.Name, o.NameEn, o.Type.ToString(), o.Code, o.IsActive, o.Notes,
                db.ExternalContacts.Count(c => c.OrganizationId == o.Id), o.LogoUrl))
            .ToListAsync(ct);

        return new PagedResult<ExternalOrgDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<Result<ExternalOrgDto>> GetOrgAsync(long id, CancellationToken ct = default)
    {
        var o = await db.ExternalOrganizations.FirstOrDefaultAsync(x => x.Id == id, ct);
        return o is null ? Result<ExternalOrgDto>.Fail("الجهة غير موجودة") : Result<ExternalOrgDto>.Ok(MapOrg(o));
    }

    public async Task<Result<ExternalOrgDto>> CreateOrgAsync(CreateExternalOrgDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return Result<ExternalOrgDto>.Fail("اسم الجهة مطلوب");
        if (!string.IsNullOrWhiteSpace(dto.Code) && await db.ExternalOrganizations.AnyAsync(o => o.Code == dto.Code, ct))
            return Result<ExternalOrgDto>.Fail("رمز الجهة مستخدم مسبقًا");

        if (NormalizeLogo(dto.LogoUrl) is { } logoError) return Result<ExternalOrgDto>.Fail(logoError);

        var o = new ExternalOrganization
        {
            Name = dto.Name, NameEn = dto.NameEn, Type = dto.Type, Code = dto.Code, Notes = dto.Notes,
            LogoUrl = Trimmed(dto.LogoUrl),
        };
        db.ExternalOrganizations.Add(o);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "ExternalOrganization", o.Id, o.Name, ct: ct);
        return Result<ExternalOrgDto>.Ok(MapOrg(o));
    }

    public async Task<Result<ExternalOrgDto>> UpdateOrgAsync(long id, CreateExternalOrgDto dto, CancellationToken ct = default)
    {
        var o = await db.ExternalOrganizations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (o is null) return Result<ExternalOrgDto>.Fail("الجهة غير موجودة");
        if (!string.IsNullOrWhiteSpace(dto.Code) && await db.ExternalOrganizations.AnyAsync(x => x.Code == dto.Code && x.Id != id, ct))
            return Result<ExternalOrgDto>.Fail("رمز الجهة مستخدم مسبقًا");

        if (NormalizeLogo(dto.LogoUrl) is { } logoError) return Result<ExternalOrgDto>.Fail(logoError);

        o.Name = dto.Name; o.NameEn = dto.NameEn; o.Type = dto.Type; o.Code = dto.Code; o.Notes = dto.Notes;
        // Sent empty, the logo is removed — that is how the picker's «إزالة» reaches the column.
        o.LogoUrl = Trimmed(dto.LogoUrl);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", "ExternalOrganization", o.Id, o.Name, ct: ct);
        return Result<ExternalOrgDto>.Ok(MapOrg(o));
    }

    public async Task<Result<bool>> DeleteOrgAsync(long id, CancellationToken ct = default)
    {
        var o = await db.ExternalOrganizations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (o is null) return Result<bool>.Fail("الجهة غير موجودة");
        db.ExternalOrganizations.Remove(o);   // soft-delete via interceptor
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Delete", "ExternalOrganization", id, o.Name, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ── Contacts ───────────────────────────────────────────────────────────────
    public async Task<PagedResult<ExternalContactDto>> ListContactsAsync(long? organizationId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 50 : pageSize;

        var q = db.ExternalContacts.Include(c => c.Channels).Include(c => c.Organization).AsQueryable();
        if (organizationId is { } orgId) q = q.Where(c => c.OrganizationId == orgId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(c => c.Name.Contains(s) || (c.NameEn != null && c.NameEn.Contains(s)) || (c.Title != null && c.Title.Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(c => c.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = rows.Select(MapContact).ToList();
        return new PagedResult<ExternalContactDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<Result<ExternalContactDto>> CreateContactAsync(CreateExternalContactDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return Result<ExternalContactDto>.Fail("اسم جهة الاتصال مطلوب");
        if (dto.OrganizationId is { } orgId && !await db.ExternalOrganizations.AnyAsync(o => o.Id == orgId, ct))
            return Result<ExternalContactDto>.Fail("الجهة غير موجودة");

        var c = new ExternalContact { OrganizationId = dto.OrganizationId, Name = dto.Name, NameEn = dto.NameEn, Title = dto.Title };
        ApplyChannels(c, dto.Channels);
        db.ExternalContacts.Add(c);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "ExternalContact", c.Id, c.Name, ct: ct);
        return Result<ExternalContactDto>.Ok(await LoadContactAsync(c.Id, ct));
    }

    public async Task<Result<ExternalContactDto>> UpdateContactAsync(long id, CreateExternalContactDto dto, CancellationToken ct = default)
    {
        var c = await db.ExternalContacts.Include(x => x.Channels).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<ExternalContactDto>.Fail("جهة الاتصال غير موجودة");

        c.OrganizationId = dto.OrganizationId; c.Name = dto.Name; c.NameEn = dto.NameEn; c.Title = dto.Title;
        db.ContactChannels.RemoveRange(c.Channels);
        c.Channels.Clear();
        ApplyChannels(c, dto.Channels);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Edited", "ExternalContact", c.Id, c.Name, ct: ct);
        return Result<ExternalContactDto>.Ok(await LoadContactAsync(c.Id, ct));
    }

    public async Task<Result<bool>> DeleteContactAsync(long id, CancellationToken ct = default)
    {
        var c = await db.ExternalContacts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return Result<bool>.Fail("جهة الاتصال غير موجودة");
        db.ExternalContacts.Remove(c);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Delete", "ExternalContact", id, c.Name, ct: ct);
        return Result<bool>.Ok(true);
    }

    // ── helpers ────────────────────────────────────────────────────────────────
    private static void ApplyChannels(ExternalContact c, IReadOnlyList<CreateContactChannelDto>? channels)
    {
        if (channels is null) return;
        foreach (var ch in channels.Where(x => !string.IsNullOrWhiteSpace(x.Value)))
            c.Channels.Add(new ContactChannel { Type = ch.Type, Value = ch.Value.Trim(), IsPrimary = ch.IsPrimary });
    }

    private async Task<ExternalContactDto> LoadContactAsync(long id, CancellationToken ct) =>
        MapContact(await db.ExternalContacts.Include(c => c.Channels).Include(c => c.Organization).FirstAsync(c => c.Id == id, ct));

    private static ExternalOrgDto MapOrg(ExternalOrganization o) =>
        new(o.Id, o.Name, o.NameEn, o.Type.ToString(), o.Code, o.IsActive, o.Notes, o.Contacts?.Count ?? 0, o.LogoUrl);

    private static string? Trimmed(string? logo) => string.IsNullOrWhiteSpace(logo) ? null : logo.Trim();

    /// <summary>Returns the reason a logo is unacceptable, or null when it is fine.
    ///
    /// Inline images only, for the reason on <see cref="ExternalOrganization.LogoUrl"/>: a remote
    /// URL would make the directory pull content from an arbitrary host on every render.</summary>
    private static string? NormalizeLogo(string? logo)
    {
        var trimmed = Trimmed(logo);
        if (trimmed is null) return null;
        if (!trimmed.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return "صيغة الصورة غير مدعومة";
        if (trimmed.Length > ExternalOrganization.LogoMaxChars)
            return "حجم الصورة كبير جدّاً";
        return null;
    }

    private static ExternalContactDto MapContact(ExternalContact c) => new(
        c.Id, c.OrganizationId, c.Organization?.Name, c.Name, c.NameEn, c.Title, c.IsActive,
        c.Channels.OrderByDescending(ch => ch.IsPrimary)
            .Select(ch => new ContactChannelDto(ch.Id, ch.Type.ToString(), ch.Value, ch.IsPrimary)).ToList());
}
