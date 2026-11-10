using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PerTypeFieldsAndDesigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DocumentTypeId",
                table: "ReportTemplates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MailCategoryId",
                table: "ReportTemplates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DocumentTypeId",
                table: "CustomFieldDefinitions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplates_DocumentTypeId",
                table: "ReportTemplates",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplates_MailCategoryId",
                table: "ReportTemplates",
                column: "MailCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_DocumentTypeId",
                table: "CustomFieldDefinitions",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_EntityType_DocumentTypeId",
                table: "CustomFieldDefinitions",
                columns: new[] { "EntityType", "DocumentTypeId" });

            migrationBuilder.AddForeignKey(
                name: "FK_CustomFieldDefinitions_DocumentTypes_DocumentTypeId",
                table: "CustomFieldDefinitions",
                column: "DocumentTypeId",
                principalTable: "DocumentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportTemplates_DocumentTypes_DocumentTypeId",
                table: "ReportTemplates",
                column: "DocumentTypeId",
                principalTable: "DocumentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportTemplates_MailCategories_MailCategoryId",
                table: "ReportTemplates",
                column: "MailCategoryId",
                principalTable: "MailCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CustomFieldDefinitions_DocumentTypes_DocumentTypeId",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportTemplates_DocumentTypes_DocumentTypeId",
                table: "ReportTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_ReportTemplates_MailCategories_MailCategoryId",
                table: "ReportTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ReportTemplates_DocumentTypeId",
                table: "ReportTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ReportTemplates_MailCategoryId",
                table: "ReportTemplates");

            migrationBuilder.DropIndex(
                name: "IX_CustomFieldDefinitions_DocumentTypeId",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CustomFieldDefinitions_EntityType_DocumentTypeId",
                table: "CustomFieldDefinitions");

            migrationBuilder.DropColumn(
                name: "DocumentTypeId",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "MailCategoryId",
                table: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "DocumentTypeId",
                table: "CustomFieldDefinitions");
        }
    }
}
