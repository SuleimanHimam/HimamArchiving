using Archiving.Domain.Core;
using Archiving.Domain.Entities;
using Archiving.Domain.Supply;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Archiving.Infrastructure.Persistence.Configurations;

// Supply schema. Quantities decimal(18,3) (consumables may be fractional: kilo, gallon), money decimal(18,2).
// Every reference to an employee, unit, site or user is a foreign key into core / the archive tables —
// nothing is copied (standards §3.3).

internal static class SupplySchema
{
    public const string Name = "supply";
    public static PropertyBuilder<decimal> SupplyQty(this PropertyBuilder<decimal> p) => p.HasPrecision(18, 3);
    public static PropertyBuilder<decimal?> SupplyQty(this PropertyBuilder<decimal?> p) => p.HasPrecision(18, 3);
}

internal sealed class SupplySettingConfiguration : IEntityTypeConfiguration<SupplySetting>
{
    public void Configure(EntityTypeBuilder<SupplySetting> b)
    {
        b.Property(x => x.Key).HasMaxLength(64).IsRequired();
        b.Property(x => x.Value).HasMaxLength(500).IsRequired();
        b.HasIndex(x => x.Key).IsUnique();
        b.MapPlatformTable(SupplySchema.Name, "settings");
    }
}

internal sealed class ItemCategoryConfiguration : IEntityTypeConfiguration<ItemCategory>
{
    public void Configure(EntityTypeBuilder<ItemCategory> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasOne<ItemCategory>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "item_categories");
    }
}

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(32).IsRequired();
        b.Property(x => x.ReorderLevel).SupplyQty();
        b.Property(x => x.MinLevel).SupplyQty();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "items");
    }
}

internal sealed class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> b)
    {
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.KeeperEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "warehouses");
    }
}

internal sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> b)
    {
        b.Property(x => x.AssetNumber).HasMaxLength(32).IsRequired();
        b.Property(x => x.Barcode).HasMaxLength(64).IsRequired();
        b.Property(x => x.SerialNumber).HasMaxLength(100);
        b.Property(x => x.Value).HasPrecision(18, 2);
        b.HasIndex(x => x.AssetNumber).IsUnique();
        b.HasIndex(x => x.Barcode).IsUnique();
        b.HasIndex(x => x.SerialNumber).HasFilter("[serial_number] IS NOT NULL");
        b.HasIndex(x => x.Status);
        b.HasOne(x => x.Item).WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "assets");
    }
}

internal sealed class CustodyConfiguration : IEntityTypeConfiguration<Custody>
{
    public void Configure(EntityTypeBuilder<Custody> b)
    {
        b.HasIndex(x => new { x.AssetId, x.ToDate });
        b.HasIndex(x => x.EmployeeId);
        b.HasIndex(x => x.OrgUnitId);
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SupplyDocument>().WithMany().HasForeignKey(x => x.OpenedByDocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SupplyDocument>().WithMany().HasForeignKey(x => x.ClosedByDocumentId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "custodies");
    }
}

internal sealed class SupplyDocumentConfiguration : IEntityTypeConfiguration<SupplyDocument>
{
    public void Configure(EntityTypeBuilder<SupplyDocument> b)
    {
        b.Property(x => x.Number).HasMaxLength(32);
        b.Property(x => x.Title).HasMaxLength(500).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.Supplier).HasMaxLength(200);
        b.Property(x => x.InvoiceNumber).HasMaxLength(64);
        b.Property(x => x.ReceivedByName).HasMaxLength(200);
        b.Property(x => x.DecisionNote).HasMaxLength(2000);
        b.HasIndex(x => x.Number).IsUnique().HasFilter("[number] IS NOT NULL");
        b.HasIndex(x => new { x.Type, x.Status });
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.TargetWarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.RecipientEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.RecipientOrgUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<IssueRequest>().WithMany().HasForeignKey(x => x.IssueRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CountCycle>().WithMany().HasForeignKey(x => x.CountCycleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OpeningImport>().WithMany().HasForeignKey(x => x.OpeningImportId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "documents");
    }
}

internal sealed class SupplyDocumentLineConfiguration : IEntityTypeConfiguration<SupplyDocumentLine>
{
    public void Configure(EntityTypeBuilder<SupplyDocumentLine> b)
    {
        b.Property(x => x.Quantity).SupplyQty();
        b.Property(x => x.SerialNumber).HasMaxLength(100);
        b.Property(x => x.Barcode).HasMaxLength(64);
        b.Property(x => x.UnitValue).HasPrecision(18, 2);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.HolderEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.HolderOrgUnitId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "document_lines");
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.Property(x => x.Quantity).SupplyQty();
        b.HasIndex(x => new { x.ItemId, x.WarehouseId });
        b.HasIndex(x => x.MovementDate);
        b.HasIndex(x => x.OrgUnitId);
        b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<SupplyDocument>().WithMany().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkOrder>().WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "stock_movements");
    }
}

internal sealed class IssueRequestConfiguration : IEntityTypeConfiguration<IssueRequest>
{
    public void Configure(EntityTypeBuilder<IssueRequest> b)
    {
        b.Property(x => x.Number).HasMaxLength(32);
        b.Property(x => x.Justification).HasMaxLength(1000).IsRequired();
        b.Property(x => x.DecisionNote).HasMaxLength(2000);
        b.HasIndex(x => x.Number).IsUnique().HasFilter("[number] IS NOT NULL");
        b.HasIndex(x => x.Status);
        b.HasIndex(x => new { x.OrgUnitId, x.MonthlyQuotaRunId });
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.RequesterEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApprovalRequest>().WithMany().HasForeignKey(x => x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Lines).WithOne().HasForeignKey(l => l.IssueRequestId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "issue_requests");
    }
}

internal sealed class IssueRequestLineConfiguration : IEntityTypeConfiguration<IssueRequestLine>
{
    public void Configure(EntityTypeBuilder<IssueRequestLine> b)
    {
        b.Property(x => x.Quantity).SupplyQty();
        b.Property(x => x.IssuedQuantity).SupplyQty();
        b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "issue_request_lines");
    }
}

internal sealed class MonthlyQuotaConfiguration : IEntityTypeConfiguration<MonthlyQuota>
{
    public void Configure(EntityTypeBuilder<MonthlyQuota> b)
    {
        b.Property(x => x.Quantity).SupplyQty();
        b.HasIndex(x => new { x.OrgUnitId, x.ItemId }).IsUnique().HasFilter("[is_deleted] = 0");
        b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "monthly_quotas");
    }
}

internal sealed class CountCycleConfiguration : IEntityTypeConfiguration<CountCycle>
{
    public void Configure(EntityTypeBuilder<CountCycle> b)
    {
        b.Property(x => x.Number).HasMaxLength(32);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.HasIndex(x => x.Number).IsUnique().HasFilter("[number] IS NOT NULL");
        b.HasIndex(x => x.Status);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Committee).WithOne().HasForeignKey(m => m.CountCycleId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "count_cycles");
    }
}

internal sealed class CountCommitteeMemberConfiguration : IEntityTypeConfiguration<CountCommitteeMember>
{
    public void Configure(EntityTypeBuilder<CountCommitteeMember> b)
    {
        b.HasIndex(x => new { x.CountCycleId, x.EmployeeId }).IsUnique().HasFilter("[is_deleted] = 0");
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "count_committee_members");
    }
}

internal sealed class CountEntryConfiguration : IEntityTypeConfiguration<CountEntry>
{
    public void Configure(EntityTypeBuilder<CountEntry> b)
    {
        b.Property(x => x.Quantity).SupplyQty();
        b.Property(x => x.Barcode).HasMaxLength(64);
        b.Property(x => x.Note).HasMaxLength(500);
        b.HasIndex(x => x.CountCycleId);
        b.HasOne<CountCycle>().WithMany().HasForeignKey(x => x.CountCycleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "count_entries");
    }
}

internal sealed class CountVarianceConfiguration : IEntityTypeConfiguration<CountVariance>
{
    public void Configure(EntityTypeBuilder<CountVariance> b)
    {
        b.Property(x => x.Expected).SupplyQty();
        b.Property(x => x.Counted).SupplyQty();
        b.Property(x => x.Difference).SupplyQty();
        b.Property(x => x.Barcode).HasMaxLength(64);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.Justification).HasMaxLength(1000);
        b.HasIndex(x => x.CountCycleId);
        b.HasOne<CountCycle>().WithMany().HasForeignKey(x => x.CountCycleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Item>().WithMany().HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "count_variances");
    }
}

internal sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> b)
    {
        b.Property(x => x.Number).HasMaxLength(32).IsRequired();
        b.Property(x => x.Location).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        b.Property(x => x.Assessment).HasMaxLength(1000);
        b.Property(x => x.Outcome).HasMaxLength(1000);
        b.Property(x => x.LaborHours).HasPrecision(9, 2);
        b.Property(x => x.LaborCost).HasPrecision(18, 2);
        b.Property(x => x.PartsCost).HasPrecision(18, 2);
        b.HasIndex(x => x.Number).IsUnique();
        b.HasIndex(x => x.Status);
        b.HasOne<Asset>().WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Site>().WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.ReporterEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.AssigneeEmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(SupplySchema.Name, "work_orders");
    }
}

internal sealed class OpeningImportConfiguration : IEntityTypeConfiguration<OpeningImport>
{
    public void Configure(EntityTypeBuilder<OpeningImport> b)
    {
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.Stage).HasMaxLength(200).IsRequired();
        b.Property(x => x.ErrorsJson).IsRequired();
        b.MapPlatformTable(SupplySchema.Name, "opening_balance_imports");
    }
}
