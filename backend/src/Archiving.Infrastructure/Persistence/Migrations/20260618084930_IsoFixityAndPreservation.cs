using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IsoFixityAndPreservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing attachments were hashed with SHA-256 at ingest — backfill accordingly.
            migrationBuilder.AddColumn<string>(
                name: "ChecksumAlgorithm",
                table: "DocumentAttachments",
                type: "varchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "SHA-256");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFixityCheckAt",
                table: "DocumentAttachments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FixityChecks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DocumentAttachmentId = table.Column<long>(type: "bigint", nullable: false),
                    Algorithm = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    ExpectedHash = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    ActualHash = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Result = table.Column<int>(type: "int", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CheckedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixityChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixityChecks_DocumentAttachments_DocumentAttachmentId",
                        column: x => x.DocumentAttachmentId,
                        principalTable: "DocumentAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FixityChecks_CheckedAt",
                table: "FixityChecks",
                column: "CheckedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FixityChecks_DocumentAttachmentId_CheckedAt",
                table: "FixityChecks",
                columns: new[] { "DocumentAttachmentId", "CheckedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FixityChecks");

            migrationBuilder.DropColumn(
                name: "ChecksumAlgorithm",
                table: "DocumentAttachments");

            migrationBuilder.DropColumn(
                name: "LastFixityCheckAt",
                table: "DocumentAttachments");
        }
    }
}
