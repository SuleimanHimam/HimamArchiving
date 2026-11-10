using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DiwanRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CurrentDiwanId",
                table: "IncomingMails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiwanMailNumber",
                table: "IncomingMails",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExternalOrganizationId",
                table: "IncomingMails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FromDiwanId",
                table: "IncomingMails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Origin",
                table: "IncomingMails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceTo",
                table: "IncomingMails",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Diwans",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Diwans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Diwans_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Diwans_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingMails_CurrentDiwanId",
                table: "IncomingMails",
                column: "CurrentDiwanId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomingMails_Origin",
                table: "IncomingMails",
                column: "Origin");

            migrationBuilder.CreateIndex(
                name: "IX_Diwans_InstitutionId",
                table: "Diwans",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Diwans_OrgUnitId",
                table: "Diwans",
                column: "OrgUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Diwans");

            migrationBuilder.DropIndex(
                name: "IX_IncomingMails_CurrentDiwanId",
                table: "IncomingMails");

            migrationBuilder.DropIndex(
                name: "IX_IncomingMails_Origin",
                table: "IncomingMails");

            migrationBuilder.DropColumn(
                name: "CurrentDiwanId",
                table: "IncomingMails");

            migrationBuilder.DropColumn(
                name: "DiwanMailNumber",
                table: "IncomingMails");

            migrationBuilder.DropColumn(
                name: "ExternalOrganizationId",
                table: "IncomingMails");

            migrationBuilder.DropColumn(
                name: "FromDiwanId",
                table: "IncomingMails");

            migrationBuilder.DropColumn(
                name: "Origin",
                table: "IncomingMails");

            migrationBuilder.DropColumn(
                name: "ReferenceTo",
                table: "IncomingMails");
        }
    }
}
