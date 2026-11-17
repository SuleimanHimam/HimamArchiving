using Archiving.Application.Common.Interfaces;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Core;
using Archiving.Application.Features.Hr;
using Archiving.Domain.Core;
using Archiving.Domain.Hr;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Services.Core;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Hr;

/// <summary>
/// The personnel file (operations sequence §8.2) around the core employee row. Every call first resolves
/// the employee through the same row scope as the employee register, so an out-of-scope employee is
/// indistinguishable from a missing one. Sensitive values are stored encrypted, returned masked, and
/// revealed only through <see cref="RevealAsync"/>, which is audited.
/// </summary>
public sealed class EmployeeFileService(
    AppDbContext db,
    IAccessScope scope,
    IAuditWriter audit,
    ISensitiveDataProtector protector) : IEmployeeFileService
{
    private const string NotFound = "الموظف غير موجود";
    private const string RowNotFound = "السجل غير موجود";

    public async Task<Result<EmployeeFileDto>> GetAsync(long employeeId, CancellationToken ct = default)
    {
        var employee = await FindEmployeeAsync(employeeId, ct);
        if (employee is null) return Result<EmployeeFileDto>.Fail(NotFound);

        var core = await new EmployeeService(db, audit, scope).GetAsync(employeeId, ct);
        if (!core.Succeeded) return Result<EmployeeFileDto>.Fail(NotFound);

        var profile = await db.EmployeeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.EmployeeId == employeeId, ct);
        var family = await db.EmployeeFamilyMembers.AsNoTracking().Where(f => f.EmployeeId == employeeId)
            .OrderBy(f => f.Id).ToListAsync(ct);
        var quals = await db.EmployeeQualifications.AsNoTracking().Where(q => q.EmployeeId == employeeId)
            .OrderBy(q => q.Id).ToListAsync(ct);
        var banks = await db.EmployeeBankAccounts.AsNoTracking().Where(b => b.EmployeeId == employeeId)
            .OrderByDescending(b => b.IsPrimary).ThenBy(b => b.Id).ToListAsync(ct);
        var actions = await db.PersonnelActions.AsNoTracking().Include(a => a.ActionType)
            .Where(a => a.EmployeeId == employeeId).OrderByDescending(a => a.EffectiveDate).ThenByDescending(a => a.Id)
            .ToListAsync(ct);

        var bankDtos = new List<BankAccountDto>(banks.Count);
        foreach (var b in banks) bankDtos.Add(await ToDtoAsync(b, ct));

        return Result<EmployeeFileDto>.Ok(new EmployeeFileDto(
            core.Value!, await ToDtoAsync(profile, ct),
            family.Select(ToDto).ToList(), quals.Select(ToDto).ToList(), bankDtos,
            await PersonnelActionService.ToDtosAsync(db, actions, ct)));
    }

    // ---- Profile ----

    public async Task<Result<ProfileDto>> SaveProfileAsync(long employeeId, SaveProfileRequest r, CancellationToken ct = default)
    {
        if (await FindEmployeeAsync(employeeId, ct) is null) return Result<ProfileDto>.Fail(NotFound);

        var p = await db.EmployeeProfiles.FirstOrDefaultAsync(x => x.EmployeeId == employeeId, ct);
        var isNew = p is null;
        if (!isNew && !CoreGuards.IsCurrent(p!.RowVersion, r.RowVersion)) return Result<ProfileDto>.Fail(CoreGuards.Stale);
        p ??= new EmployeeProfile { EmployeeId = employeeId };

        var address = CoreGuards.CleanOrNull(r.Address);
        if (address?.Length > 500) return Result<ProfileDto>.Fail("العنوان أطول من 500 حرف");
        if (r.BirthDate is { } bd && (bd > DateOnly.FromDateTime(DateTime.UtcNow) || bd.Year < 1900))
            return Result<ProfileDto>.Fail("تاريخ الميلاد غير صالح");
        if (r.MaritalStatusId is { } ms && !await LookupService.IsValidAsync(db, ms, LookupCategories.MaritalStatus, ct))
            return Result<ProfileDto>.Fail("الحالة الاجتماعية غير صالحة");

        // Grade and appointment type are set once from launch data; after that only an approved
        // Promote/Confirm action changes them (§8.3).
        if (r.GradeId != p.GradeId)
        {
            if (p.GradeId is not null) return Result<ProfileDto>.Fail("الدرجة تتغير عبر حركة ترقية معتمدة");
            if (r.GradeId is { } g && !await LookupService.IsValidAsync(db, g, LookupCategories.Grade, ct))
                return Result<ProfileDto>.Fail("الدرجة غير صالحة");
        }
        if (r.AppointmentTypeId != p.AppointmentTypeId)
        {
            if (p.AppointmentTypeId is not null) return Result<ProfileDto>.Fail("نوع التعيين يتغير عبر حركة تثبيت معتمدة");
            if (r.AppointmentTypeId is { } at && !await LookupService.IsValidAsync(db, at, LookupCategories.AppointmentType, ct))
                return Result<ProfileDto>.Fail("نوع التعيين غير صالح");
        }

        var nationalId = r.NationalId?.Trim();
        var mobile = r.Mobile?.Trim();
        if (nationalId is { Length: > 32 }) return Result<ProfileDto>.Fail("رقم الهوية أطول من 32 حرفاً");
        if (mobile is { Length: > 32 }) return Result<ProfileDto>.Fail("رقم الجوال أطول من 32 حرفاً");

        p.BirthDate = r.BirthDate;
        p.MaritalStatusId = r.MaritalStatusId;
        p.Address = address;
        p.GradeId = r.GradeId;
        p.AppointmentTypeId = r.AppointmentTypeId;
        if (nationalId is not null) p.NationalIdEnc = nationalId.Length == 0 ? null : await protector.ProtectAsync(nationalId, ct);
        if (mobile is not null) p.MobileEnc = mobile.Length == 0 ? null : await protector.ProtectAsync(mobile, ct);

        if (isNew) db.EmployeeProfiles.Add(p);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<ProfileDto>.Fail(CoreGuards.Stale); }

        // Sensitive values are never written to the audit trail; only that they changed.
        await audit.WriteAsync(isNew ? "Created" : "Updated", "EmployeeProfile", employeeId, null,
            newValues: CoreGuards.Json(new
            {
                p.BirthDate, p.MaritalStatusId, p.GradeId, p.AppointmentTypeId,
                NationalIdChanged = nationalId is not null, MobileChanged = mobile is not null,
            }), ct: ct);
        return Result<ProfileDto>.Ok(await ToDtoAsync(p, ct));
    }

    // ---- Family ----

    public async Task<Result<FamilyMemberDto>> AddFamilyMemberAsync(long employeeId, SaveFamilyMemberRequest r, CancellationToken ct = default)
    {
        if (await FindEmployeeAsync(employeeId, ct) is null) return Result<FamilyMemberDto>.Fail(NotFound);
        if (await ValidateAsync(r, ct) is { } error) return Result<FamilyMemberDto>.Fail(error);
        var e = new EmployeeFamilyMember { EmployeeId = employeeId };
        Apply(e, r);
        db.EmployeeFamilyMembers.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "EmployeeFamilyMember", e.Id, e.FullName, ct: ct);
        return Result<FamilyMemberDto>.Ok(ToDto(e));
    }

    public async Task<Result<FamilyMemberDto>> UpdateFamilyMemberAsync(long employeeId, long id, SaveFamilyMemberRequest r, CancellationToken ct = default)
    {
        if (await FindEmployeeAsync(employeeId, ct) is null) return Result<FamilyMemberDto>.Fail(NotFound);
        var e = await db.EmployeeFamilyMembers.FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId, ct);
        if (e is null) return Result<FamilyMemberDto>.Fail(RowNotFound);
        if (!CoreGuards.IsCurrent(e.RowVersion, r.RowVersion)) return Result<FamilyMemberDto>.Fail(CoreGuards.Stale);
        if (await ValidateAsync(r, ct) is { } error) return Result<FamilyMemberDto>.Fail(error);
        Apply(e, r);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<FamilyMemberDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Updated", "EmployeeFamilyMember", e.Id, e.FullName, ct: ct);
        return Result<FamilyMemberDto>.Ok(ToDto(e));
    }

    public Task<Result<bool>> DeleteFamilyMemberAsync(long employeeId, long id, CancellationToken ct = default) =>
        SoftDeleteAsync(db.EmployeeFamilyMembers, employeeId, id, "EmployeeFamilyMember", ct);

    // ---- Qualifications ----

    public async Task<Result<QualificationDto>> AddQualificationAsync(long employeeId, SaveQualificationRequest r, CancellationToken ct = default)
    {
        if (await FindEmployeeAsync(employeeId, ct) is null) return Result<QualificationDto>.Fail(NotFound);
        if (await ValidateAsync(r, ct) is { } error) return Result<QualificationDto>.Fail(error);
        var e = new EmployeeQualification { EmployeeId = employeeId };
        Apply(e, r);
        db.EmployeeQualifications.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "EmployeeQualification", e.Id, e.Title, ct: ct);
        return Result<QualificationDto>.Ok(ToDto(e));
    }

    public async Task<Result<QualificationDto>> UpdateQualificationAsync(long employeeId, long id, SaveQualificationRequest r, CancellationToken ct = default)
    {
        if (await FindEmployeeAsync(employeeId, ct) is null) return Result<QualificationDto>.Fail(NotFound);
        var e = await db.EmployeeQualifications.FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId, ct);
        if (e is null) return Result<QualificationDto>.Fail(RowNotFound);
        if (!CoreGuards.IsCurrent(e.RowVersion, r.RowVersion)) return Result<QualificationDto>.Fail(CoreGuards.Stale);
        if (await ValidateAsync(r, ct) is { } error) return Result<QualificationDto>.Fail(error);
        Apply(e, r);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<QualificationDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Updated", "EmployeeQualification", e.Id, e.Title, ct: ct);
        return Result<QualificationDto>.Ok(ToDto(e));
    }

    public Task<Result<bool>> DeleteQualificationAsync(long employeeId, long id, CancellationToken ct = default) =>
        SoftDeleteAsync(db.EmployeeQualifications, employeeId, id, "EmployeeQualification", ct);

    // ---- Bank accounts ----

    public async Task<Result<BankAccountDto>> AddBankAccountAsync(long employeeId, SaveBankAccountRequest r, CancellationToken ct = default)
    {
        if (await FindEmployeeAsync(employeeId, ct) is null) return Result<BankAccountDto>.Fail(NotFound);
        var account = r.Account?.Trim() ?? string.Empty;
        if (account.Length is 0 or > 64) return Result<BankAccountDto>.Fail("رقم الحساب مطلوب ولا يتجاوز 64 حرفاً");
        if (ValidateBank(r) is { } error) return Result<BankAccountDto>.Fail(error);

        var e = new EmployeeBankAccount
        {
            EmployeeId = employeeId, BankName = CoreGuards.Clean(r.BankName), IsPrimary = r.IsPrimary,
            AccountEnc = await protector.ProtectAsync(account, ct),
        };
        if (e.IsPrimary) await ClearOtherPrimaryAsync(employeeId, null, ct);
        db.EmployeeBankAccounts.Add(e);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Created", "EmployeeBankAccount", e.Id, e.BankName, ct: ct);
        return Result<BankAccountDto>.Ok(await ToDtoAsync(e, ct));
    }

    public async Task<Result<BankAccountDto>> UpdateBankAccountAsync(long employeeId, long id, SaveBankAccountRequest r, CancellationToken ct = default)
    {
        if (await FindEmployeeAsync(employeeId, ct) is null) return Result<BankAccountDto>.Fail(NotFound);
        var e = await db.EmployeeBankAccounts.FirstOrDefaultAsync(x => x.Id == id && x.EmployeeId == employeeId, ct);
        if (e is null) return Result<BankAccountDto>.Fail(RowNotFound);
        if (!CoreGuards.IsCurrent(e.RowVersion, r.RowVersion)) return Result<BankAccountDto>.Fail(CoreGuards.Stale);
        if (ValidateBank(r) is { } error) return Result<BankAccountDto>.Fail(error);
        var account = r.Account?.Trim();
        if (account is { Length: 0 or > 64 }) return Result<BankAccountDto>.Fail("رقم الحساب لا يكون فارغاً ولا يتجاوز 64 حرفاً");

        e.BankName = CoreGuards.Clean(r.BankName);
        e.IsPrimary = r.IsPrimary;
        if (account is not null) e.AccountEnc = await protector.ProtectAsync(account, ct);
        if (e.IsPrimary) await ClearOtherPrimaryAsync(employeeId, e.Id, ct);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<BankAccountDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Updated", "EmployeeBankAccount", e.Id, e.BankName,
            newValues: CoreGuards.Json(new { AccountChanged = account is not null }), ct: ct);
        return Result<BankAccountDto>.Ok(await ToDtoAsync(e, ct));
    }

    public Task<Result<bool>> DeleteBankAccountAsync(long employeeId, long id, CancellationToken ct = default) =>
        SoftDeleteAsync(db.EmployeeBankAccounts, employeeId, id, "EmployeeBankAccount", ct);

    // ---- Reveal ----

    public async Task<Result<RevealDto>> RevealAsync(long employeeId, RevealRequest r, CancellationToken ct = default)
    {
        if (await FindEmployeeAsync(employeeId, ct) is null) return Result<RevealDto>.Fail(NotFound);

        byte[]? blob = r.Field switch
        {
            SensitiveFields.NationalId => (await db.EmployeeProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.EmployeeId == employeeId, ct))?.NationalIdEnc,
            SensitiveFields.Mobile => (await db.EmployeeProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.EmployeeId == employeeId, ct))?.MobileEnc,
            SensitiveFields.BankAccount when r.RecordId is { } bankId => (await db.EmployeeBankAccounts.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == bankId && b.EmployeeId == employeeId, ct))?.AccountEnc,
            _ => null,
        };
        if (blob is null) return Result<RevealDto>.Fail("لا توجد قيمة لعرضها");

        var value = await protector.UnprotectAsync(blob, ct);
        // Standards §5.3/§7.3: viewing sensitive data is audited — who, which employee, which field. Never the value.
        await audit.WriteAsync("SensitiveRevealed", "Employee", employeeId, null,
            newValues: CoreGuards.Json(new { r.Field, r.RecordId }), ct: ct);
        return Result<RevealDto>.Ok(new RevealDto(value));
    }

    // ---- internals ----

    private async Task<Employee?> FindEmployeeAsync(long employeeId, CancellationToken ct) =>
        await (await EmployeeService.ScopedQueryAsync(db, scope, ct)).FirstOrDefaultAsync(e => e.Id == employeeId, ct);

    private async Task<Result<bool>> SoftDeleteAsync<T>(DbSet<T> set, long employeeId, long id, string entity, CancellationToken ct)
        where T : CoreEntity
    {
        if (await FindEmployeeAsync(employeeId, ct) is null) return Result<bool>.Fail(NotFound);
        var e = await set.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null || EmployeeIdOf(e) != employeeId) return Result<bool>.Fail(RowNotFound);
        set.Remove(e);   // soft delete via the interceptor
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Deleted", entity, id, ct: ct);
        return Result<bool>.Ok(true);
    }

    private static long EmployeeIdOf(CoreEntity e) => e switch
    {
        EmployeeFamilyMember f => f.EmployeeId,
        EmployeeQualification q => q.EmployeeId,
        EmployeeBankAccount b => b.EmployeeId,
        _ => -1,
    };

    private async Task ClearOtherPrimaryAsync(long employeeId, long? keepId, CancellationToken ct)
    {
        var others = await db.EmployeeBankAccounts.Where(b => b.EmployeeId == employeeId && b.IsPrimary && b.Id != keepId).ToListAsync(ct);
        foreach (var o in others) o.IsPrimary = false;
    }

    private async Task<string?> ValidateAsync(SaveFamilyMemberRequest r, CancellationToken ct)
    {
        if (CoreGuards.Clean(r.FullName).Length is 0 or > 200) return "الاسم مطلوب ولا يتجاوز 200 حرف";
        if (CoreGuards.Clean(r.Notes).Length > 500) return "الملاحظات أطول من 500 حرف";
        if (r.BirthDate is { } bd && bd > DateOnly.FromDateTime(DateTime.UtcNow)) return "تاريخ الميلاد غير صالح";
        return await LookupService.IsValidAsync(db, r.RelationshipId, LookupCategories.Relationship, ct) ? null : "صلة القرابة غير صالحة";
    }

    private async Task<string?> ValidateAsync(SaveQualificationRequest r, CancellationToken ct)
    {
        if (CoreGuards.Clean(r.Title).Length is 0 or > 200) return "اسم المؤهل مطلوب ولا يتجاوز 200 حرف";
        if (CoreGuards.Clean(r.Institution).Length > 200) return "اسم الجهة أطول من 200 حرف";
        if (r.Year is { } y && (y < 1950 || y > DateTime.UtcNow.Year)) return "سنة المؤهل غير صالحة";
        return await LookupService.IsValidAsync(db, r.QualificationTypeId, LookupCategories.QualificationType, ct) ? null : "نوع المؤهل غير صالح";
    }

    private static string? ValidateBank(SaveBankAccountRequest r) =>
        CoreGuards.Clean(r.BankName).Length is 0 or > 150 ? "اسم البنك مطلوب ولا يتجاوز 150 حرفاً" : null;

    private static void Apply(EmployeeFamilyMember e, SaveFamilyMemberRequest r)
    {
        e.FullName = CoreGuards.Clean(r.FullName);
        e.RelationshipId = r.RelationshipId;
        e.BirthDate = r.BirthDate;
        e.Notes = CoreGuards.CleanOrNull(r.Notes);
    }

    private static void Apply(EmployeeQualification e, SaveQualificationRequest r)
    {
        e.QualificationTypeId = r.QualificationTypeId;
        e.Title = CoreGuards.Clean(r.Title);
        e.Institution = CoreGuards.CleanOrNull(r.Institution);
        e.Year = r.Year;
    }

    private async Task<ProfileDto> ToDtoAsync(EmployeeProfile? p, CancellationToken ct) => p is null
        ? new ProfileDto(null, null, null, null, null, null, null, null)
        : new ProfileDto(p.BirthDate, p.MaritalStatusId, p.Address, p.AppointmentTypeId, p.GradeId,
            await MaskAsync(p.NationalIdEnc, ct), await MaskAsync(p.MobileEnc, ct), Convert.ToBase64String(p.RowVersion));

    private async Task<BankAccountDto> ToDtoAsync(EmployeeBankAccount b, CancellationToken ct) =>
        new(b.Id, b.BankName, await MaskAsync(b.AccountEnc, ct) ?? string.Empty, b.IsPrimary, Convert.ToBase64String(b.RowVersion));

    /// <summary>The mask is computed from the decrypted value on the server; the clear value never leaves it.</summary>
    private async Task<string?> MaskAsync(byte[]? blob, CancellationToken ct) =>
        blob is null ? null : ISensitiveDataProtector.Mask(await protector.UnprotectAsync(blob, ct));

    private static FamilyMemberDto ToDto(EmployeeFamilyMember f) =>
        new(f.Id, f.FullName, f.RelationshipId, f.BirthDate, f.Notes, Convert.ToBase64String(f.RowVersion));

    private static QualificationDto ToDto(EmployeeQualification q) =>
        new(q.Id, q.QualificationTypeId, q.Title, q.Institution, q.Year, Convert.ToBase64String(q.RowVersion));
}
