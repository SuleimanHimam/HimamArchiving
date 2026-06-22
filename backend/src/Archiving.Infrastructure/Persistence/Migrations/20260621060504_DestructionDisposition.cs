using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DestructionDisposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DestroyedAtUtc",
                table: "Documents",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DestructionCertificateId",
                table: "Documents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTombstone",
                table: "Documents",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DestructionCertificates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DestructionRequestId = table.Column<long>(type: "bigint", nullable: false),
                    CertificateNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    PdfStorageKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestructionCertificates", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DestructionRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "longtext", maxLength: 256, nullable: false),
                    RetentionBasisId = table.Column<long>(type: "bigint", nullable: true),
                    RequestedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReviewedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ApprovedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ExecutedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DecisionNote = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    ScheduledForUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CertificateId = table.Column<long>(type: "bigint", nullable: true),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestructionRequests", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LegalHolds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Reason = table.Column<string>(type: "longtext", maxLength: 256, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: true),
                    FolderId = table.Column<long>(type: "bigint", nullable: true),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    QueryExpression = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    PlacedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    PlacedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReleasedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalHolds", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DestructionItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DestructionRequestId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    ChecksumBefore = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Outcome = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestructionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DestructionItems_DestructionRequests_DestructionRequestId",
                        column: x => x.DestructionRequestId,
                        principalTable: "DestructionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DestructionCertificates_DestructionRequestId",
                table: "DestructionCertificates",
                column: "DestructionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DestructionItems_DestructionRequestId",
                table: "DestructionItems",
                column: "DestructionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DestructionItems_DocumentId",
                table: "DestructionItems",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DestructionRequests_Status",
                table: "DestructionRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_ReleasedAtUtc",
                table: "LegalHolds",
                column: "ReleasedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_Scope_DocumentId",
                table: "LegalHolds",
                columns: new[] { "Scope", "DocumentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DestructionCertificates");

            migrationBuilder.DropTable(
                name: "DestructionItems");

            migrationBuilder.DropTable(
                name: "LegalHolds");

            migrationBuilder.DropTable(
                name: "DestructionRequests");

            migrationBuilder.DropColumn(
                name: "DestroyedAtUtc",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DestructionCertificateId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "IsTombstone",
                table: "Documents");
        }
    }
}
