using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReportTemplatesAndLogoOverride : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoOverrideBase64",
                table: "OutgoingMails",
                type: "longtext",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoOverrideBase64",
                table: "IncomingMails",
                type: "longtext",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReportTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Target = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    LogoBase64 = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    LayoutJson = table.Column<string>(type: "longtext", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportTemplates_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportTemplates_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplates_InstitutionId_Scope_Target",
                table: "ReportTemplates",
                columns: new[] { "InstitutionId", "Scope", "Target" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplates_OrgUnitId",
                table: "ReportTemplates",
                column: "OrgUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReportTemplates");

            migrationBuilder.DropColumn(
                name: "LogoOverrideBase64",
                table: "OutgoingMails");

            migrationBuilder.DropColumn(
                name: "LogoOverrideBase64",
                table: "IncomingMails");
        }
    }
}
