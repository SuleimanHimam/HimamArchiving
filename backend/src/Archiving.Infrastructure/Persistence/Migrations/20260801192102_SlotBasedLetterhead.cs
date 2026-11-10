using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SlotBasedLetterhead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ApprovalWorkflowInstanceId",
                table: "ReportTemplates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyLayoutJson",
                table: "ReportTemplates",
                type: "longtext",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "ReportTemplates",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlotsJson",
                table: "ReportTemplates",
                type: "longtext",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "ReportTemplates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplates_InstitutionId_Target_Status",
                table: "ReportTemplates",
                columns: new[] { "InstitutionId", "Target", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportTemplates_InstitutionId_Target_Status",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "ApprovalWorkflowInstanceId",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "LegacyLayoutJson",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "SlotsJson",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ReportTemplates");
        }
    }
}
