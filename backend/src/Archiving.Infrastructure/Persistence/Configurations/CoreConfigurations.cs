using System.Text;
using Archiving.Domain.Core;
using Archiving.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Archiving.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping conventions for tables that follow the unified-platform standard: an explicit schema,
/// snake_case names, soft delete and a row version. The archive's own tables predate the standard and
/// are not touched by this.
/// </summary>
internal static class PlatformTable
{
    public const string CoreSchema = "core";

    public static void MapPlatformTable<T>(this EntityTypeBuilder<T> b, string schema, string table)
        where T : CoreEntity
    {
        b.ToTable(table, schema);
        b.HasQueryFilter(x => !x.IsDeleted);
        b.Property(x => x.RowVersion).IsRowVersion();

        // Every scalar column, including the audit columns inherited from the base entity.
        foreach (var property in b.Metadata.GetProperties())
            property.SetColumnName(ToSnakeCase(property.Name));
    }

    /// <summary>"EmployeeNumber" → "employee_number", "OrgUnitId" → "org_unit_id".</summary>
    internal static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }
}

internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.MapPlatformTable(PlatformTable.CoreSchema, "sites");
    }
}

internal sealed class SiteLocationConfiguration : IEntityTypeConfiguration<SiteLocation>
{
    public void Configure(EntityTypeBuilder<SiteLocation> b)
    {
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.NameEn).HasMaxLength(200);
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.HasIndex(x => new { x.SiteId, x.Code }).IsUnique();
        b.HasOne(x => x.Site).WithMany(s => s.Locations).HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);
        b.MapPlatformTable(PlatformTable.CoreSchema, "site_locations");
    }
}

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> b)
    {
        b.Property(x => x.EmployeeNumber).HasMaxLength(32).IsRequired();
        b.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        b.Property(x => x.FullNameEn).HasMaxLength(200);
        b.Property(x => x.JobTitle).HasMaxLength(150).IsRequired();

        b.HasIndex(x => x.EmployeeNumber).IsUnique();
        // One account per employee. Filtered, so any number of employees may have no account yet.
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("[user_id] IS NOT NULL");
        b.HasIndex(x => x.OrgUnitId);
        b.HasIndex(x => x.SiteId);
        b.HasIndex(x => x.Status);

        // Restrict everywhere: an employee, a unit or a site is never removed out from under the
        // records that point at it.
        b.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.OrgUnitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Position>().WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Site).WithMany().HasForeignKey(x => x.SiteId).OnDelete(DeleteBehavior.Restrict);

        b.MapPlatformTable(PlatformTable.CoreSchema, "employees");
    }
}
