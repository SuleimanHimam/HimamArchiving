using System.Text.Json;
using Archiving.Application.Common.Interfaces;
using Archiving.Application.Features.Core;
using Archiving.Application.Features.Hr;
using Archiving.Domain.Common;
using Archiving.Domain.Core;
using Archiving.Domain.Hr;
using Archiving.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Infrastructure.Services.Hr;

/// <summary>
/// Applies an approved personnel action (operations sequence §8.3). Runs inside the approval transaction:
/// throwing <see cref="DomainException"/> rolls the approval back. The decision number is issued here, on
/// execution, so a rolled-back approval leaves no gap.
/// </summary>
public sealed class PersonnelActionEffectHandler(
    AppDbContext db,
    INumberingService numbering,
    IAuditWriter audit) : IApprovalEffect
{
    public const string ActionNumbering = "PersonnelAction";
    public const string ClearanceNumbering = "Clearance";

    public string RequestType => PersonnelActionService.RequestType;

    public async Task OnApprovedAsync(ApprovalRequest request, CancellationToken ct)
    {
        var a = await LoadAsync(request, ct);
        if (a.Status != PersonnelActionStatus.Submitted) throw new DomainException("الحركة ليست بانتظار الاعتماد");
        var p = PersonnelActionService.Payload(a);

        object? before = null, after = null;
        switch (a.ActionType.Effect)
        {
            case PersonnelActionEffect.Hire:
                (before, after) = await HireAsync(a, p, ct);
                break;

            case PersonnelActionEffect.Confirm:
            {
                var profile = await ProfileAsync(a, ct);
                before = new { profile.AppointmentTypeId };
                profile.AppointmentTypeId = p.AppointmentTypeId;
                after = new { profile.AppointmentTypeId };
                break;
            }

            case PersonnelActionEffect.Promote:
            {
                var profile = await ProfileAsync(a, ct);
                before = new { profile.GradeId };
                profile.GradeId = p.GradeId;
                after = new { profile.GradeId };
                break;
            }

            case PersonnelActionEffect.Transfer:
            {
                var e = await EmployeeAsync(a, ct);
                before = new { e.OrgUnitId, e.PositionId, e.JobTitle, e.SiteId };
                e.OrgUnitId = p.OrgUnitId!.Value;
                e.PositionId = p.PositionId ?? e.PositionId;
                e.JobTitle = p.JobTitle ?? e.JobTitle;
                e.SiteId = p.SiteId ?? e.SiteId;
                // The login's department is what the access scope reads, so it moves with the employee.
                if (e.UserId is { } uid && await db.Users.FirstOrDefaultAsync(u => u.Id == uid, ct) is { } user)
                    user.OrgUnitId = e.OrgUnitId;
                after = new { e.OrgUnitId, e.PositionId, e.JobTitle, e.SiteId };
                break;
            }

            case PersonnelActionEffect.Suspend:
            {
                var e = await EmployeeAsync(a, ct);
                before = new { Status = e.Status.ToString() };
                e.Status = EmployeeStatus.Suspended;
                await SetLoginActiveAsync(e, false, ct);
                after = new { Status = e.Status.ToString(), LoginDisabled = e.UserId is not null };
                break;
            }

            case PersonnelActionEffect.EndService:
            {
                var e = await EmployeeAsync(a, ct);
                before = new { Status = e.Status.ToString() };
                e.Status = EmployeeStatus.Terminated;
                var clearance = await OpenClearanceAsync(e, a, ct);
                // The login stays until the clearance is issued (§8.1: clearance, settlement, then disable).
                after = new { Status = e.Status.ToString(), ClearanceNumber = clearance };
                break;
            }

            // Recorded on the file; the payroll and permissions consequences are built with those modules.
            case PersonnelActionEffect.Second:
            case PersonnelActionEffect.Allowance:
            case PersonnelActionEffect.Penalty:
                after = p;
                break;

            default:
                throw new DomainException("أثر الحركة غير معرّف");
        }

        a.Number = await numbering.GenerateNumberAsync(ActionNumbering, ct: ct)
            ?? throw new DomainException("قالب ترقيم الحركات الإدارية غير معرّف");
        a.Status = PersonnelActionStatus.Executed;
        a.ExecutedAt = DateTime.UtcNow;
        a.BeforeJson = before is null ? null : JsonSerializer.Serialize(before, PersonnelActionService.Json);
        a.AfterJson = after is null ? null : JsonSerializer.Serialize(after, PersonnelActionService.Json);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Executed", "PersonnelAction", a.Id, a.Number, a.BeforeJson, a.AfterJson, ct);
    }

    public async Task OnRejectedAsync(ApprovalRequest request, string reason, CancellationToken ct)
    {
        var a = await LoadAsync(request, ct);
        a.Status = PersonnelActionStatus.Rejected;
        a.DecisionNote = reason;
        await audit.WriteAsync("Rejected", "PersonnelAction", a.Id, a.ActionType.Name, newValues: reason, ct: ct);
    }

    public async Task OnReturnedAsync(ApprovalRequest request, string reason, CancellationToken ct)
    {
        var a = await LoadAsync(request, ct);
        a.Status = PersonnelActionStatus.Returned;
        a.DecisionNote = reason;
        await audit.WriteAsync("Returned", "PersonnelAction", a.Id, a.ActionType.Name, newValues: reason, ct: ct);
    }

    // ---- effects ----

    private async Task<(object, object)> HireAsync(PersonnelAction a, PersonnelActionPayload p, CancellationToken ct)
    {
        // Re-checked at execution: the number may have been taken while the request was in the path.
        if (await db.Employees.IgnoreQueryFilters().AnyAsync(e => e.EmployeeNumber == p.EmployeeNumber, ct))
            throw new DomainException("الرقم الوظيفي مستخدم. أعد الحركة لتصحيحه");
        if (p.OrgUnitId is not { } unitId || !await db.OrgUnits.AnyAsync(u => u.Id == unitId, ct))
            throw new DomainException("الوحدة التنظيمية غير موجودة");

        var employee = new Employee
        {
            EmployeeNumber = p.EmployeeNumber!,
            FullName = p.FullName!,
            FullNameEn = p.FullNameEn,
            Category = Enum.Parse<EmployeeCategory>(p.Category!, ignoreCase: true),
            Status = EmployeeStatus.Active,
            OrgUnitId = unitId,
            PositionId = p.PositionId,
            JobTitle = p.JobTitle!,
            SiteId = p.SiteId,
            HireDate = a.EffectiveDate,
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync(ct);

        db.EmployeeProfiles.Add(new EmployeeProfile
        {
            EmployeeId = employee.Id, GradeId = p.GradeId, AppointmentTypeId = p.AppointmentTypeId,
        });
        a.EmployeeId = employee.Id;
        await audit.WriteAsync("Created", "Employee", employee.Id, employee.FullName, ct: ct);
        return (new { }, new { EmployeeId = employee.Id, employee.EmployeeNumber });
    }

    private async Task<string> OpenClearanceAsync(Employee e, PersonnelAction a, CancellationToken ct)
    {
        var parties = await db.Lookups.Where(l => l.Category == LookupCategories.ClearanceParty && l.IsActive)
            .OrderBy(l => l.SortOrder).Select(l => l.Id).ToListAsync(ct);
        if (parties.Count == 0) throw new DomainException("لا توجد جهات براءة ذمة معرّفة في الإعدادات");

        var number = await numbering.GenerateNumberAsync(ClearanceNumbering, ct: ct)
            ?? throw new DomainException("قالب ترقيم براءة الذمة غير معرّف");
        var clearance = new Clearance { Number = number, EmployeeId = e.Id, PersonnelActionId = a.Id };
        foreach (var party in parties) clearance.Items.Add(new ClearanceItem { PartyId = party });
        db.Clearances.Add(clearance);
        await audit.WriteAsync("Opened", "Clearance", e.Id, number, ct: ct);
        return number;
    }

    internal static async Task SetLoginActiveAsync(AppDbContext db, Employee e, bool active, CancellationToken ct)
    {
        if (e.UserId is { } uid && await db.Users.FirstOrDefaultAsync(u => u.Id == uid, ct) is { } user)
            user.IsActive = active;
    }

    private Task SetLoginActiveAsync(Employee e, bool active, CancellationToken ct) => SetLoginActiveAsync(db, e, active, ct);

    private async Task<PersonnelAction> LoadAsync(ApprovalRequest request, CancellationToken ct) =>
        await db.PersonnelActions.Include(x => x.ActionType).FirstOrDefaultAsync(x => x.Id == request.SourceId, ct)
        ?? throw new DomainException("الحركة الإدارية غير موجودة");

    private async Task<Employee> EmployeeAsync(PersonnelAction a, CancellationToken ct) =>
        await db.Employees.FirstOrDefaultAsync(e => e.Id == a.EmployeeId, ct)
        ?? throw new DomainException("الموظف غير موجود");

    private async Task<EmployeeProfile> ProfileAsync(PersonnelAction a, CancellationToken ct)
    {
        var employee = await EmployeeAsync(a, ct);
        var profile = await db.EmployeeProfiles.FirstOrDefaultAsync(x => x.EmployeeId == employee.Id, ct);
        if (profile is not null) return profile;
        profile = new EmployeeProfile { EmployeeId = employee.Id };
        db.EmployeeProfiles.Add(profile);
        return profile;
    }
}
