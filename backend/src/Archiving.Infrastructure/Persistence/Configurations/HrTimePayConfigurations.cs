using Archiving.Domain.Core;
using Archiving.Domain.Entities;
using Archiving.Domain.Hr;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Archiving.Infrastructure.Persistence.Configurations;

// HR phases 2–3: leave, shifts, attendance, payroll. Money decimal(18,2), quantities decimal(9,2),
// rates decimal(18,4). Same conventions as HrConfigurations.

internal static class HrPrecision
{
    public static PropertyBuilder<decimal> Money(this PropertyBuilder<decimal> p) => p.HasPrecision(18, 2);
    public static PropertyBuilder<decimal?> Money(this PropertyBuilder<decimal?> p) => p.HasPrecision(18, 2);
    public static PropertyBuilder<decimal> Qty(this PropertyBuilder<decimal> p) => p.HasPrecision(9, 2);
    public static PropertyBuilder<decimal?> Qty(this PropertyBuilder<decimal?> p) => p.HasPrecision(9, 2);
    public static PropertyBuilder<decimal> Rate(this PropertyBuilder<decimal> p) => p.HasPrecision(18, 4);
}

internal sealed class HrSettingConfiguration : IEntityTypeConfiguration<HrSetting>
{
    public void Configure(EntityTypeBuilder<HrSetting> b)
    {
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.Property(x => x.Value).HasMaxLength(500).IsRequired();
        b.HasIndex(x => x.Key).IsUnique();
        b.MapPlatformTable(HrSchema.Name, "settings");
    }
}

internal sealed class PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>
{
    public void Configure(EntityTypeBuilder<PublicHoliday> b)
    {
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Date).IsUnique().HasFilter("[is_deleted] = 0");
        b.MapPlatformTable(HrSchema.Name, "public_holidays");
    }
}

internal sealed class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.AnnualEntitlement).Qty();
        b.Property(x => x.MaxCarryOver).Qty();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "leave_types");
    }
}

internal sealed class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> b)
    {
        b.Property(x => x.Number).HasMaxLength(32);
        b.Property(x => x.Quantity).Qty();
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.Property(x => x.DecisionNote).HasMaxLength(2000);
        b.HasIndex(x => x.Number).IsUnique().HasFilter("[number] IS NOT NULL");
        b.HasIndex(x => new { x.EmployeeId, x.FromDate });
        b.HasIndex(x => x.Status);
        b.HasOne(x => x.LeaveType).WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.SubstituteEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "leave_requests");
    }
}

internal sealed class LeaveBalanceMovementConfiguration : IEntityTypeConfiguration<LeaveBalanceMovement>
{
    public void Configure(EntityTypeBuilder<LeaveBalanceMovement> b)
    {
        b.Property(x => x.Quantity).Qty();
        b.Property(x => x.SourceType).HasMaxLength(64).IsRequired();
        b.Property(x => x.Note).HasMaxLength(1000);
        b.HasIndex(x => new { x.EmployeeId, x.LeaveTypeId, x.Year });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LeaveType>().WithMany().HasForeignKey(x => x.LeaveTypeId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "leave_balance_movements");
    }
}

internal sealed class ShiftTypeConfiguration : IEntityTypeConfiguration<ShiftType>
{
    public void Configure(EntityTypeBuilder<ShiftType> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Ignore(x => x.CrossesMidnight);
        b.HasIndex(x => x.Code).IsUnique();
        b.MapPlatformTable(HrSchema.Name, "shift_types");
    }
}

internal sealed class ShiftAssignmentConfiguration : IEntityTypeConfiguration<ShiftAssignment>
{
    public void Configure(EntityTypeBuilder<ShiftAssignment> b)
    {
        b.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique().HasFilter("[is_deleted] = 0");
        b.HasIndex(x => new { x.SiteId, x.Date });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ShiftType>().WithMany().HasForeignKey(x => x.ShiftTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<LeaveRequest>().WithMany().HasForeignKey(x => x.LeaveRequestId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "shift_assignments");
    }
}

internal sealed class ShiftRosterConfiguration : IEntityTypeConfiguration<ShiftRoster>
{
    public void Configure(EntityTypeBuilder<ShiftRoster> b)
    {
        b.HasIndex(x => new { x.SiteId, x.Year, x.Month }).IsUnique().HasFilter("[is_deleted] = 0");
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "shift_rosters");
    }
}

internal sealed class CoverageRuleConfiguration : IEntityTypeConfiguration<CoverageRule>
{
    public void Configure(EntityTypeBuilder<CoverageRule> b)
    {
        b.HasIndex(x => new { x.SiteId, x.ShiftTypeId }).IsUnique().HasFilter("[is_deleted] = 0");
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ShiftType>().WithMany().HasForeignKey(x => x.ShiftTypeId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "coverage_rules");
    }
}

internal sealed class AttendancePunchConfiguration : IEntityTypeConfiguration<AttendancePunch>
{
    public void Configure(EntityTypeBuilder<AttendancePunch> b)
    {
        b.Property(x => x.DeviceCode).HasMaxLength(64);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.HasIndex(x => new { x.EmployeeId, x.PunchedAt });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "attendance_punches");
    }
}

internal sealed class AttendanceExceptionConfiguration : IEntityTypeConfiguration<AttendanceException>
{
    public void Configure(EntityTypeBuilder<AttendanceException> b)
    {
        b.Property(x => x.ResolutionNote).HasMaxLength(1000);
        b.HasIndex(x => new { x.EmployeeId, x.Date, x.Kind }).IsUnique().HasFilter("[is_deleted] = 0");
        b.HasIndex(x => new { x.Status, x.Date });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ShiftType>().WithMany().HasForeignKey(x => x.ShiftTypeId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "attendance_exceptions");
    }
}

internal sealed class AttendancePeriodConfiguration : IEntityTypeConfiguration<AttendancePeriod>
{
    public void Configure(EntityTypeBuilder<AttendancePeriod> b)
    {
        b.HasIndex(x => new { x.Year, x.Month }).IsUnique().HasFilter("[is_deleted] = 0");
        b.MapPlatformTable(HrSchema.Name, "attendance_periods");
    }
}

// ---- payroll ----

internal sealed class PayElementConfiguration : IEntityTypeConfiguration<PayElement>
{
    public void Configure(EntityTypeBuilder<PayElement> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.MapPlatformTable(HrSchema.Name, "pay_elements");
    }
}

internal sealed class PayElementRateConfiguration : IEntityTypeConfiguration<PayElementRate>
{
    public void Configure(EntityTypeBuilder<PayElementRate> b)
    {
        b.Property(x => x.Value).Rate();
        b.HasIndex(x => new { x.PayElementId, x.EffectiveFrom });
        b.HasOne<PayElement>().WithMany().HasForeignKey(x => x.PayElementId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "pay_element_rates");
    }
}

internal sealed class SalaryScaleRateConfiguration : IEntityTypeConfiguration<SalaryScaleRate>
{
    public void Configure(EntityTypeBuilder<SalaryScaleRate> b)
    {
        b.Property(x => x.BasicAmount).Money();
        b.HasIndex(x => new { x.GradeId, x.EffectiveFrom });
        b.HasOne<Lookup>().WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "salary_scale_rates");
    }
}

internal sealed class EmployeePayElementConfiguration : IEntityTypeConfiguration<EmployeePayElement>
{
    public void Configure(EntityTypeBuilder<EmployeePayElement> b)
    {
        b.Property(x => x.Amount).Money();
        b.HasIndex(x => new { x.EmployeeId, x.PayElementId });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayElement>().WithMany().HasForeignKey(x => x.PayElementId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "employee_pay_elements");
    }
}

internal sealed class TaxBracketConfiguration : IEntityTypeConfiguration<TaxBracket>
{
    public void Configure(EntityTypeBuilder<TaxBracket> b)
    {
        b.Property(x => x.FromAmount).Money();
        b.Property(x => x.ToAmount).Money();
        b.Property(x => x.Rate).Rate();
        b.HasIndex(x => x.EffectiveFrom);
        b.MapPlatformTable(HrSchema.Name, "tax_brackets");
    }
}

internal sealed class PayrollPeriodConfiguration : IEntityTypeConfiguration<PayrollPeriod>
{
    public void Configure(EntityTypeBuilder<PayrollPeriod> b)
    {
        b.HasIndex(x => new { x.Year, x.Month }).IsUnique().HasFilter("[is_deleted] = 0");
        b.MapPlatformTable(HrSchema.Name, "payroll_periods");
    }
}

internal sealed class PayrollStageLogConfiguration : IEntityTypeConfiguration<PayrollStageLog>
{
    public void Configure(EntityTypeBuilder<PayrollStageLog> b)
    {
        b.Property(x => x.Note).HasMaxLength(1000);
        b.HasOne<PayrollPeriod>().WithMany(p => p.Log).HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "payroll_stage_log");
    }
}

internal sealed class PayrollInputConfiguration : IEntityTypeConfiguration<PayrollInput>
{
    public void Configure(EntityTypeBuilder<PayrollInput> b)
    {
        b.Property(x => x.Quantity).Rate();
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => new { x.PayrollPeriodId, x.EmployeeId });
        b.HasOne<PayrollPeriod>().WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayElement>().WithMany().HasForeignKey(x => x.PayElementId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "payroll_inputs");
    }
}

internal sealed class PayrollResultConfiguration : IEntityTypeConfiguration<PayrollResult>
{
    public void Configure(EntityTypeBuilder<PayrollResult> b)
    {
        b.Property(x => x.Gross).Money();
        b.Property(x => x.Deductions).Money();
        b.Property(x => x.Net).Money();
        b.Property(x => x.Paid).Money();
        b.HasIndex(x => new { x.PayrollPeriodId, x.EmployeeId }).IsUnique().HasFilter("[is_deleted] = 0");
        b.HasOne<PayrollPeriod>().WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "payroll_results");
    }
}

internal sealed class PayrollResultLineConfiguration : IEntityTypeConfiguration<PayrollResultLine>
{
    public void Configure(EntityTypeBuilder<PayrollResultLine> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Quantity).Rate();
        b.Property(x => x.Rate).Rate();
        b.Property(x => x.Amount).Money();
        b.HasOne<PayrollResult>().WithMany(r => r.Lines).HasForeignKey(x => x.PayrollResultId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayElement>().WithMany().HasForeignKey(x => x.PayElementId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "payroll_result_lines");
    }
}

internal sealed class DisbursementRuleConfiguration : IEntityTypeConfiguration<DisbursementRule>
{
    public void Configure(EntityTypeBuilder<DisbursementRule> b)
    {
        b.Property(x => x.PercentOfSalary).Rate();
        b.Property(x => x.MinimumAmount).Money();
        b.Property(x => x.PercentOfAccumulated).Rate();
        b.HasIndex(x => new { x.PayrollPeriodId, x.Category }).IsUnique().HasFilter("[is_deleted] = 0");
        b.HasOne<PayrollPeriod>().WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "disbursement_rules");
    }
}

internal sealed class EntitlementMovementConfiguration : IEntityTypeConfiguration<EntitlementMovement>
{
    public void Configure(EntityTypeBuilder<EntitlementMovement> b)
    {
        b.Property(x => x.Amount).Money();
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => x.EmployeeId);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PayrollPeriod>().WithMany().HasForeignKey(x => x.PayrollPeriodId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "entitlement_movements");
    }
}
