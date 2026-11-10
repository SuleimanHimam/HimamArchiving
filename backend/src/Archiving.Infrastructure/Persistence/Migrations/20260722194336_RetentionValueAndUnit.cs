using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetentionValueAndUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetentionUnit",
                table: "DocumentTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "RetentionValue",
                table: "DocumentTypes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Without this every existing type would read as "0 days". Split the stored month
            // count into a friendly unit — whole years where it divides evenly, else months.
            // RetentionUnit: 0 = Days, 1 = Months, 2 = Years. Mirrors Retention.FromMonths.
            migrationBuilder.Sql(@"
                UPDATE DocumentTypes
                SET RetentionValue = CASE WHEN RetentionMonths > 0 AND RetentionMonths % 12 = 0
                                          THEN RetentionMonths / 12 ELSE RetentionMonths END,
                    RetentionUnit  = CASE WHEN RetentionMonths > 0 AND RetentionMonths % 12 = 0
                                          THEN 2 ELSE 1 END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetentionUnit",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "RetentionValue",
                table: "DocumentTypes");
        }
    }
}
