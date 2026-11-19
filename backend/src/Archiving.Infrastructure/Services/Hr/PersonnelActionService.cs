using System.Text.Json;
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
/// Personnel actions (operations sequence §8.3): drafted by HR, sent through the approval path the
/// administrator attached to the action type, and applied by <see cref="PersonnelActionEffect"/> when
/// the last level approves.
/// </summary>
public sealed class PersonnelActionService(
    AppDbContext db,
    IAccessScope scope,
    IAuditWriter audit,
    IApprovalService approvals) : IPersonnelActionService
{
    public const string RequestType = "hr.action";
    private const string NotFound = "الحركة غير موجودة";
    private const int MaxPageSize = 200;

    public async Task<PagedResult<PersonnelActionDto>> ListAsync(PersonnelActionListQuery q, CancellationToken ct = default)
    {
        var query = await ScopedAsync(ct);
        if (q.EmployeeId is { } emp) query = query.Where(a => a.EmployeeId == emp);
        if (Enum.TryParse<PersonnelActionStatus>(q.Status, ignoreCase: true, out var status))
            query = query.Where(a => a.Status == status);

        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, MaxPageSize);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<PersonnelActionDto>
        {
            Items = await ToDtosAsync(db, rows, ct), Page = page, PageSize = pageSize, TotalCount = total,
        };
    }

    public async Task<Result<PersonnelActionDto>> GetAsync(long id, CancellationToken ct = default)
    {
        var a = await (await ScopedAsync(ct)).FirstOrDefaultAsync(x => x.Id == id, ct);
        return a is null ? Result<PersonnelActionDto>.Fail(NotFound) : Result<PersonnelActionDto>.Ok((await ToDtosAsync(db, [a], ct))[0]);
    }

    public async Task<Result<PersonnelActionDto>> CreateAsync(SavePersonnelActionRequest r, CancellationToken ct = default)
    {
        var (type, error) = await ValidateAsync(r, ct);
        if (type is null) return Result<PersonnelActionDto>.Fail(error!);

        var a = new PersonnelAction { ActionTypeId = type.Id };
        Apply(a, r);
        db.PersonnelActions.Add(a);
        await db.SaveChangesAsync(ct);
        a.ActionType = type;
        await audit.WriteAsync("Created", "PersonnelAction", a.Id, type.Name, newValues: a.PayloadJson, ct: ct);
        return Result<PersonnelActionDto>.Ok((await ToDtosAsync(db, [a], ct))[0]);
    }

    public async Task<Result<PersonnelActionDto>> UpdateAsync(long id, SavePersonnelActionRequest r, CancellationToken ct = default)
    {
        var a = await FindTrackedAsync(id, ct);
        if (a is null) return Result<PersonnelActionDto>.Fail(NotFound);
        if (a.Status is not (PersonnelActionStatus.Draft or PersonnelActionStatus.Returned))
            return Result<PersonnelActionDto>.Fail("لا تُعدَّل الحركة بعد تقديمها");
        if (!CoreGuards.IsCurrent(a.RowVersion, r.RowVersion)) return Result<PersonnelActionDto>.Fail(CoreGuards.Stale);

        var (type, error) = await ValidateAsync(r, ct);
        if (type is null) return Result<PersonnelActionDto>.Fail(error!);

        var before = a.PayloadJson;
        a.ActionTypeId = type.Id;
        a.ActionType = type;
        Apply(a, r);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<PersonnelActionDto>.Fail(CoreGuards.Stale); }
        await audit.WriteAsync("Updated", "PersonnelAction", a.Id, type.Name, before, a.PayloadJson, ct);
        return Result<PersonnelActionDto>.Ok((await ToDtosAsync(db, [a], ct))[0]);
    }

    public async Task<Result<PersonnelActionDto>> SubmitAsync(long id, CancellationToken ct = default)
    {
        var a = await FindTrackedAsync(id, ct);
        if (a is null) return Result<PersonnelActionDto>.Fail(NotFound);
        if (a.Status is not (PersonnelActionStatus.Draft or PersonnelActionStatus.Returned))
            return Result<PersonnelActionDto>.Fail("الحركة مقدّمة مسبقاً أو مغلقة");
        if (!a.ActionType.IsActive) return Result<PersonnelActionDto>.Fail("نوع الحركة غير مفعّل");
        if (a.ActionType.WorkflowDefinitionId is not { } workflowId)
            return Result<PersonnelActionDto>.Fail("لم يُحدَّد مسار موافقة لهذا النوع من الحركات. يحدده مدير النظام من إعدادات الموارد البشرية");

        // Re-validate: the employee may have changed since the draft was written.
        var (_, error) = await ValidateAsync(
            new SavePersonnelActionRequest(a.ActionTypeId, a.EmployeeId, a.EffectiveDate, a.Reason, Payload(a)), ct);
        if (error is not null) return Result<PersonnelActionDto>.Fail(error);

        var title = $"{a.ActionType.Name}: {await SubjectNameAsync(a, ct)}";
        var result = await db.InTransactionAsync(async () =>
        {
            var submitted = await approvals.SubmitAsync(
                new SubmitApprovalRequest(RequestType, "HR", a.Id, title, workflowId), ct);
            if (!submitted.Succeeded) return Result<PersonnelActionDto>.Fail(submitted.Error!);

            a.Status = PersonnelActionStatus.Submitted;
            a.ApprovalRequestId = submitted.Value!.Id;
            a.DecisionNote = null;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync("Submitted", "PersonnelAction", a.Id, title, ct: ct);
            return Result<PersonnelActionDto>.Ok((await ToDtosAsync(db, [a], ct))[0]);
        }, r => r.Succeeded, ct);
        return result;
    }

    public async Task<Result<PersonnelActionDto>> CancelAsync(long id, CancellationToken ct = default)
    {
        var a = await FindTrackedAsync(id, ct);
        if (a is null) return Result<PersonnelActionDto>.Fail(NotFound);
        if (a.Status is not (PersonnelActionStatus.Draft or PersonnelActionStatus.Returned))
            return Result<PersonnelActionDto>.Fail("لا تُلغى إلا مسودة أو حركة معادة");
        a.Status = PersonnelActionStatus.Cancelled;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Cancelled", "PersonnelAction", a.Id, a.ActionType.Name, ct: ct);
        return Result<PersonnelActionDto>.Ok((await ToDtosAsync(db, [a], ct))[0]);
    }

    // ---- internals ----

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static PersonnelActionPayload Payload(PersonnelAction a) =>
        JsonSerializer.Deserialize<PersonnelActionPayload>(a.PayloadJson, Json) ?? new PersonnelActionPayload();

    private static void Apply(PersonnelAction a, SavePersonnelActionRequest r)
    {
        a.EmployeeId = r.EmployeeId;
        a.EffectiveDate = r.EffectiveDate;
        a.Reason = CoreGuards.Clean(r.Reason);
        a.PayloadJson = JsonSerializer.Serialize(Normalize(r.Payload), Json);
    }

    private static PersonnelActionPayload Normalize(PersonnelActionPayload p) => p with
    {
        EmployeeNumber = CoreGuards.CleanOrNull(p.EmployeeNumber),
        FullName = CoreGuards.CleanOrNull(p.FullName),
        FullNameEn = CoreGuards.CleanOrNull(p.FullNameEn),
        Category = CoreGuards.CleanOrNull(p.Category),
        JobTitle = CoreGuards.CleanOrNull(p.JobTitle),
        Notes = CoreGuards.CleanOrNull(p.Notes),
    };

    /// <summary>Shape checks per effect. The same checks run again at submit and, for a hire, at execution.</summary>
    private async Task<(PersonnelActionType?, string?)> ValidateAsync(SavePersonnelActionRequest r, CancellationToken ct)
    {
        var type = await db.PersonnelActionTypes.FirstOrDefaultAsync(t => t.Id == r.ActionTypeId && t.IsActive, ct);
        if (type is null) return (null, "نوع الحركة غير موجود أو غير مفعّل");
        var reason = CoreGuards.Clean(r.Reason);
        if (reason.Length is 0 or > 2000) return (null, "سبب الحركة مطلوب ولا يتجاوز 2000 حرف");
        if (r.EffectiveDate == default) return (null, "تاريخ النفاذ مطلوب");
        var p = Normalize(r.Payload ?? new PersonnelActionPayload());
        if (p.Notes?.Length > 2000) return (null, "الملاحظات أطول من 2000 حرف");

        if (type.Effect == PersonnelActionEffect.Hire)
        {
            if (r.EmployeeId is not null) return (null, "حركة التعيين لا ترتبط بموظف قائم");
            var hireError = await ValidateHireAsync(p, ct);
            return hireError is null ? (type, null) : (null, hireError);
        }

        if (r.EmployeeId is not { } employeeId) return (null, "الموظف مطلوب");
        var employee = await (await EmployeeService.ScopedQueryAsync(db, scope, ct)).FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return (null, "الموظف غير موجود");
        if (employee.Status == EmployeeStatus.Terminated) return (null, "خدمة الموظف منتهية");

        string? error = type.Effect switch
        {
            PersonnelActionEffect.Transfer when p.OrgUnitId is not { } unit => "الوحدة التنظيمية الجديدة مطلوبة",
            PersonnelActionEffect.Transfer when p.OrgUnitId == employee.OrgUnitId && p.PositionId == employee.PositionId
                => "الموظف في هذه الوحدة أصلاً",
            PersonnelActionEffect.Promote when p.GradeId is null => "الدرجة الجديدة مطلوبة",
            PersonnelActionEffect.Confirm when p.AppointmentTypeId is null => "نوع التعيين الجديد مطلوب",
            PersonnelActionEffect.Second when p.SiteId is null || p.ToDate is null => "مكان الندب وتاريخ انتهائه مطلوبان",
            PersonnelActionEffect.Second when p.ToDate < r.EffectiveDate => "تاريخ انتهاء الندب قبل تاريخ النفاذ",
            PersonnelActionEffect.Suspend when employee.Status == EmployeeStatus.Suspended => "الموظف موقوف أصلاً",
            _ => null,
        };
        error ??= await ValidateReferencesAsync(p, ct);
        return error is null ? (type, null) : (null, error);
    }

    internal async Task<string?> ValidateHireAsync(PersonnelActionPayload p, CancellationToken ct)
    {
        if (p.EmployeeNumber is not { Length: > 0 and <= 32 }) return "الرقم الوظيفي مطلوب ولا يتجاوز 32 حرفاً";
        if (p.FullName is not { Length: > 0 and <= 200 }) return "اسم الموظف مطلوب ولا يتجاوز 200 حرف";
        if (p.JobTitle is not { Length: > 0 and <= 150 }) return "المسمى الوظيفي مطلوب ولا يتجاوز 150 حرفاً";
        if (p.FullNameEn?.Length > 200) return "الاسم بالإنجليزية أطول من 200 حرف";
        if (!Enum.TryParse<EmployeeCategory>(p.Category, ignoreCase: true, out var cat) || !Enum.IsDefined(cat))
            return "فئة الموظف غير صالحة";
        if (p.OrgUnitId is null) return "الوحدة التنظيمية مطلوبة";
        if (await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.EmployeeNumber == p.EmployeeNumber, ct))
            return "الرقم الوظيفي مستخدم";
        return await ValidateReferencesAsync(p, ct);
    }

    private async Task<string?> ValidateReferencesAsync(PersonnelActionPayload p, CancellationToken ct)
    {
        if (p.OrgUnitId is { } u && !await db.OrgUnits.AnyAsync(x => x.Id == u, ct)) return "الوحدة التنظيمية غير موجودة";
        if (p.PositionId is { } pos && !await db.Positions.AnyAsync(x => x.Id == pos, ct)) return "المقعد الوظيفي غير موجود";
        if (p.SiteId is { } s && !await db.Sites.AnyAsync(x => x.Id == s, ct)) return "مكان العمل غير موجود";
        if (p.JobTitle?.Length > 150) return "المسمى الوظيفي أطول من 150 حرفاً";
        if (p.GradeId is { } g && !await LookupService.IsValidAsync(db, g, LookupCategories.Grade, ct)) return "الدرجة غير صالحة";
        if (p.AppointmentTypeId is { } at && !await LookupService.IsValidAsync(db, at, LookupCategories.AppointmentType, ct))
            return "نوع التعيين غير صالح";
        return null;
    }

    /// <summary>Actions on employees in the caller's scope, plus hires (no employee yet) for those who may
    /// create actions at all — the permission on the endpoint is the gate for those.</summary>
    private async Task<IQueryable<PersonnelAction>> ScopedAsync(CancellationToken ct)
    {
        var q = db.PersonnelActions.AsNoTracking().Include(a => a.ActionType);
        if (await scope.IsSystemAdminAsync(ct)) return q;
        var employees = (await EmployeeService.ScopedQueryAsync(db, scope, ct)).Select(e => e.Id);
        return q.Where(a => a.EmployeeId == null || employees.Contains(a.EmployeeId.Value));
    }

    private async Task<PersonnelAction?> FindTrackedAsync(long id, CancellationToken ct)
    {
        if (await (await ScopedAsync(ct)).AnyAsync(a => a.Id == id, ct) is false) return null;
        return await db.PersonnelActions.Include(a => a.ActionType).FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    private async Task<string> SubjectNameAsync(PersonnelAction a, CancellationToken ct) => a.EmployeeId is { } id
        ? await db.Employees.Where(e => e.Id == id).Select(e => e.FullName).FirstOrDefaultAsync(ct) ?? string.Empty
        : Payload(a).FullName ?? string.Empty;

    internal static async Task<IReadOnlyList<PersonnelActionDto>> ToDtosAsync(AppDbContext db,
        IReadOnlyList<PersonnelAction> rows, CancellationToken ct)
    {
        var ids = rows.Where(r => r.EmployeeId is not null).Select(r => r.EmployeeId!.Value).Distinct().ToList();
        var names = await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.FullName, ct);
        return rows.Select(a =>
        {
            var payload = Payload(a);
            return new PersonnelActionDto(a.Id, a.Number, a.ActionTypeId, a.ActionType?.Name ?? string.Empty,
                a.ActionType?.Effect.ToString() ?? string.Empty, a.EmployeeId,
                a.EmployeeId is { } e ? names.GetValueOrDefault(e) : payload.FullName,
                a.Status.ToString(), a.EffectiveDate, a.Reason, payload, a.BeforeJson, a.AfterJson,
                a.ApprovalRequestId, a.ExecutedAt, a.DecisionNote, a.CreatedAt, Convert.ToBase64String(a.RowVersion));
        }).ToList();
    }
}
