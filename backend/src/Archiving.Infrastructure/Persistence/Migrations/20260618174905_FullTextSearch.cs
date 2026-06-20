using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FullTextSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OcrText",
                table: "DocumentAttachments",
                newName: "ExtractedText");

            migrationBuilder.AddColumn<string>(
                name: "ExtractionSource",
                table: "DocumentAttachments",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtractionStatus",
                table: "DocumentAttachments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "TextExtractedAt",
                table: "DocumentAttachments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAttachments_ExtractionStatus",
                table: "DocumentAttachments",
                column: "ExtractionStatus");

            // MySQL full-text index for searching words inside files (MATCH ... AGAINST).
            migrationBuilder.Sql(
                "ALTER TABLE `DocumentAttachments` ADD FULLTEXT INDEX `FT_DocumentAttachments_ExtractedText` (`ExtractedText`)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE `DocumentAttachments` DROP INDEX `FT_DocumentAttachments_ExtractedText`");

            migrationBuilder.DropIndex(
                name: "IX_DocumentAttachments_ExtractionStatus",
                table: "DocumentAttachments");

            migrationBuilder.DropColumn(
                name: "ExtractionSource",
                table: "DocumentAttachments");

            migrationBuilder.DropColumn(
                name: "ExtractionStatus",
                table: "DocumentAttachments");

            migrationBuilder.DropColumn(
                name: "TextExtractedAt",
                table: "DocumentAttachments");

            migrationBuilder.RenameColumn(
                name: "ExtractedText",
                table: "DocumentAttachments",
                newName: "OcrText");
        }
    }
}
