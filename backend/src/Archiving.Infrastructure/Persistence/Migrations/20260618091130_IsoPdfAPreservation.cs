using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IsoPdfAPreservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "DocumentAttachments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PdfAConformance",
                table: "DocumentAttachments",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreservationNote",
                table: "DocumentAttachments",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PreservationValidated",
                table: "DocumentAttachments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "SourceAttachmentId",
                table: "DocumentAttachments",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "DocumentAttachments");

            migrationBuilder.DropColumn(
                name: "PdfAConformance",
                table: "DocumentAttachments");

            migrationBuilder.DropColumn(
                name: "PreservationNote",
                table: "DocumentAttachments");

            migrationBuilder.DropColumn(
                name: "PreservationValidated",
                table: "DocumentAttachments");

            migrationBuilder.DropColumn(
                name: "SourceAttachmentId",
                table: "DocumentAttachments");
        }
    }
}
