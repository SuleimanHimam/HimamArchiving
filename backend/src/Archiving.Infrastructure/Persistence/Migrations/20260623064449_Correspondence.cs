using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Correspondence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Correspondences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Reference = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentTitle = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    FromOrgUnitId = table.Column<long>(type: "bigint", nullable: false),
                    ResponsibleOfficerUserId = table.Column<long>(type: "bigint", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ResponseDeadline = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Subject = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "longtext", maxLength: 256, nullable: false),
                    CertificateReference = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Correspondences", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CorrespondenceRecipients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CorrespondenceId = table.Column<long>(type: "bigint", nullable: false),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: false),
                    DeliveredAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AcknowledgedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Comment = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    RespondedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceRecipients_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceRecipients_CorrespondenceId_OrgUnitId",
                table: "CorrespondenceRecipients",
                columns: new[] { "CorrespondenceId", "OrgUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceRecipients_OrgUnitId",
                table: "CorrespondenceRecipients",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_DocumentId",
                table: "Correspondences",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_Reference",
                table: "Correspondences",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_Status",
                table: "Correspondences",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrespondenceRecipients");

            migrationBuilder.DropTable(
                name: "Correspondences");
        }
    }
}
