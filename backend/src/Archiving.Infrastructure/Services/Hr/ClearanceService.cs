using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Hr;
using Archiving.Domain.Hr;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Hr;

/// <summary>
/// براءة الذمة (operations sequence §8.4). Opened by an approved end of service with one item per
/// configured party; issued only when every item is cleared, and then the employee's login is disabled.
/// </summary>
public sealed class ClearanceService(AppDbContext db, IAccessScope scope, ICurrentUser currentUser, IAuditWriter audit)
    : IClearanceService
{
    private const string NotFound = "براءة الذمة غير موجودة";

    public async Task<IReadOnlyList<ClearanceDto>> ListAsync(string? status, CancellationToken ct = default)
    {
        var q = await ScopedAsync(ct);
        if (Enum.TryParse<ClearanceStatus>(status, ignoreCase: true, out var s)) q = q.Where(c => c.Status == s);
        return await ToDtosAsync(await q.OrderByDescending(c => c.Id).Take(500).ToListAsync(ct), ct);
    }

    public async Task<Result<ClearanceDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var c = await (await ScopedAsync(ct)).FirstOrDefaultAsync(x => x.Id == id, ct);
        return c is null ? Result<ClearanceDto>.Fail(NotFound) : Result<ClearanceDto>.Ok((await ToDtosAsync([c], ct))[0]);
    }

    public async Task<Result<ClearanceDto>> ClearItemAsync(long clearanceId, long itemId, ClearItemRequest r, CancellationToken ct = default)
    {
        if (!await (await ScopedAsync(ct)).AnyAsync(c => c.Id == clearanceId, ct)) return Result<ClearanceDto>.Fail(NotFound);
        var note = CoreGuards.CleanOrNull(r.Note);
        if (note?.Length > 1000) return Result<ClearanceDto>.Fail("الملاحظة أطول من 1000 حرف");

        return await db.InTransactionAsync(async () =>
        {
            var c = await db.Clearances.Include(x => x.Items).FirstAsync(x => x.Id == clearanceId, ct);
            if (c.Status == ClearanceStatus.Issued) return Result<ClearanceDto>.Fail("براءة الذمة صادرة مسبقاً");
            var item = c.Items.FirstOrDefault(i => i.Id == itemId);
            if (item is null) return Result<ClearanceDto>.Fail("البند غير موجود");
            if (item.IsCleared) return Result<ClearanceDto>.Fail("البند مُخلى مسبقاً");

            item.IsCleared = true;
            item.ClearedAt = DateTime.UtcNow;
            item.ClearedBy = currentUser.UserId;
            item.Note = note;
            await audit.WriteAsync("ItemCleared", "Clearance", c.Id, c.Number, newValues: CoreGuards.Json(new { item.PartyId, note }), ct: ct);

            if (c.Items.All(i => i.IsCleared))
            {
                c.Status = ClearanceStatus.Issued;
                c.IssuedAt = DateTime.UtcNow;
                var employee = await db.Employees.FirstAsync(e => e.Id == c.EmployeeId, ct);
                await PersonnelActionEffectHandler.SetLoginActiveAsync(db, employee, false, ct);
                await audit.WriteAsync("Issued", "Clearance", c.Id, c.Number, ct: ct);
            }

            await db.SaveChangesAsync(ct);
            return Result<ClearanceDto>.Ok((await ToDtosAsync([c], ct))[0]);
        }, x => x.Succeeded, ct);
    }

    private async Task<IQueryable<Clearance>> ScopedAsync(CancellationToken ct)
    {
        var q = db.Clearances.AsNoTracking().Include(c => c.Items).AsQueryable();
        if (await scope.IsSystemAdminAsync(ct)) return q;
        // Terminated employees stay in their last unit, so the same scope reaches them.
        var employees = (await EmployeeService.ScopedQueryAsync(db, scope, ct)).Select(e => e.Id);
        return q.Where(c => employees.Contains(c.EmployeeId));
    }

    private async Task<IReadOnlyList<ClearanceDto>> ToDtosAsync(IReadOnlyList<Clearance> rows, CancellationToken ct)
    {
        var empIds = rows.Select(c => c.EmployeeId).Distinct().ToList();
        var partyIds = rows.SelectMany(c => c.Items).Select(i => i.PartyId).Distinct().ToList();
        var names = await db.Employees.AsNoTracking().Where(e => empIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName, ct);
        // Includes soft-deleted parties: an old clearance still names who cleared it.
        var parties = await db.Lookups.IgnoreQueryFilters().AsNoTracking().Where(l => partyIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        return rows.Select(c => new ClearanceDto(c.Id, c.Number, c.EmployeeId, names.GetValueOrDefault(c.EmployeeId),
            c.Status.ToString(), c.IssuedAt,
            c.Items.OrderBy(i => i.Id).Select(i => new ClearanceItemDto(i.Id, i.PartyId,
                parties.GetValueOrDefault(i.PartyId) ?? string.Empty, i.IsCleared, i.ClearedAt, i.Note)).ToList())).ToList();
    }
}
