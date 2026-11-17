using Archiving.Domain.Core;
using Archiving.Domain.Entities;
using Archiving.Domain.Hr;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Archiving.Infrastructure.Persistence.Configurations;

// Core reference tables and the HR schema. Same conventions as CoreConfigurations: snake_case,
// soft delete, row_version, Restrict on every foreign key.

internal static class HrSchema
{
    public const string Name = "hr";
}

internal sealed class LookupConfiguration : IEntityTypeConfiguration<Lookup>
{
    public void Configure(EntityTypeBuilder<Lookup> b)
    {
        b.Property(x => x.Category).HasMaxLength(64).IsRequired();
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.HasIndex(x => new { x.Category, x.Code }).IsUnique();
        b.MapPlatformTable(PlatformTable.CoreSchema, "lookups");
    }
}

internal sealed class ApprovalRequestConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> b)
    {
        b.Property(x => x.Number).HasMaxLength(32).IsRequired();
        b.Property(x => x.RequestType).HasMaxLength(64).IsRequired();
        b.Property(x => x.SourceModule).HasMaxLength(32).IsRequired();
        b.Property(x => x.Title).HasMaxLength(300).IsRequired();
        b.Property(x => x.DecisionNote).HasMaxLength(2000);
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => new { x.SourceModule, x.SourceId });
        b.HasIndex(x => x.WorkflowInstanceId);
        b.HasOne<WorkflowInstance>().WithMany().HasForeignKey(x => x.WorkflowInstanceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RequesterUserId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(PlatformTable.CoreSchema, "approval_requests");
    }
}

internal sealed class PersonnelActionTypeConfiguration : IEntityTypeConfiguration<PersonnelActionType>
{
    public void Configure(EntityTypeBuilder<PersonnelActionType> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "personnel_action_types");
    }
}

internal sealed class EmployeeProfileConfiguration : IEntityTypeConfiguration<EmployeeProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeProfile> b)
    {
        b.Property(x => x.Address).HasMaxLength(500);
        b.Property(x => x.NationalIdEnc).HasMaxLength(512);
        b.Property(x => x.MobileEnc).HasMaxLength(512);
        b.HasIndex(x => x.EmployeeId).IsUnique();
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Lookup>().WithMany().HasForeignKey(x => x.MaritalStatusId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Lookup>().WithMany().HasForeignKey(x => x.AppointmentTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Lookup>().WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "employee_profiles");
    }
}

internal sealed class EmployeeFamilyMemberConfiguration : IEntityTypeConfiguration<EmployeeFamilyMember>
{
    public void Configure(EntityTypeBuilder<EmployeeFamilyMember> b)
    {
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(500);
        b.HasIndex(x => x.EmployeeId);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Lookup>().WithMany().HasForeignKey(x => x.RelationshipId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "employee_family_members");
    }
}

internal sealed class EmployeeQualificationConfiguration : IEntityTypeConfiguration<EmployeeQualification>
{
    public void Configure(EntityTypeBuilder<EmployeeQualification> b)
    {
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Institution).HasMaxLength(200);
        b.HasIndex(x => x.EmployeeId);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Lookup>().WithMany().HasForeignKey(x => x.QualificationTypeId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "employee_qualifications");
    }
}

internal sealed class EmployeeBankAccountConfiguration : IEntityTypeConfiguration<EmployeeBankAccount>
{
    public void Configure(EntityTypeBuilder<EmployeeBankAccount> b)
    {
        b.Property(x => x.BankName).HasMaxLength(150).IsRequired();
        b.Property(x => x.AccountEnc).HasMaxLength(512).IsRequired();
        b.HasIndex(x => x.EmployeeId);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "employee_bank_accounts");
    }
}

internal sealed class PersonnelActionConfiguration : IEntityTypeConfiguration<PersonnelAction>
{
    public void Configure(EntityTypeBuilder<PersonnelAction> b)
    {
        b.Property(x => x.Number).HasMaxLength(32);
        b.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
        b.Property(x => x.PayloadJson).IsRequired();
        b.Property(x => x.DecisionNote).HasMaxLength(2000);
        b.HasIndex(x => x.Number).IsUnique().HasFilter("[number] IS NOT NULL");
        b.HasIndex(x => x.EmployeeId);
        b.HasIndex(x => x.Status);
        b.HasOne(x => x.ActionType).WithMany().HasForeignKey(x => x.ActionTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "personnel_actions");
    }
}

internal sealed class ClearanceConfiguration : IEntityTypeConfiguration<Clearance>
{
    public void Configure(EntityTypeBuilder<Clearance> b)
    {
        b.Property(x => x.Number).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => x.EmployeeId);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PersonnelAction>().WithMany().HasForeignKey(x => x.PersonnelActionId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "clearances");
    }
}

internal sealed class ClearanceItemConfiguration : IEntityTypeConfiguration<ClearanceItem>
{
    public void Configure(EntityTypeBuilder<ClearanceItem> b)
    {
        b.Property(x => x.Note).HasMaxLength(1000);
        b.HasIndex(x => new { x.ClearanceId, x.PartyId }).IsUnique();
        b.HasOne(x => x.Clearance).WithMany(c => c.Items).HasForeignKey(x => x.ClearanceId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Lookup>().WithMany().HasForeignKey(x => x.PartyId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(HrSchema.Name, "clearance_items");
    }
}
