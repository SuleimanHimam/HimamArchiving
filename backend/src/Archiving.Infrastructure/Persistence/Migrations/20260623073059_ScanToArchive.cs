using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScanToArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresPhysicalArchiving",
                table: "DocumentTypes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReturnReason",
                table: "Documents",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ArchiveWorkflowInstanceId",
                table: "Documents",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequiresPhysicalArchiving",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(
                name: "ArchiveReturnReason",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ArchiveWorkflowInstanceId",
                table: "Documents");
        }
    }
}
