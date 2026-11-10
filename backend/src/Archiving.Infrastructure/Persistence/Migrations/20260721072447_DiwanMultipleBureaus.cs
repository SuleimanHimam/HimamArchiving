using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DiwanMultipleBureaus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Order matters: create the join table and carry existing routing across BEFORE the
            // single-value column is dropped, otherwise every current الى ديوان link is lost.
            migrationBuilder.CreateTable(
                name: "DiwanBureaus",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DiwanMailId = table.Column<long>(type: "bigint", nullable: false),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiwanBureaus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiwanBureaus_DiwanMails_DiwanMailId",
                        column: x => x.DiwanMailId,
                        principalTable: "DiwanMails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiwanBureaus_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DiwanBureaus_DiwanMailId_OrgUnitId",
                table: "DiwanBureaus",
                columns: new[] { "DiwanMailId", "OrgUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiwanBureaus_OrgUnitId",
                table: "DiwanBureaus",
                column: "OrgUnitId");

            // Carry every existing single-bureau routing into the new table. The join to OrgUnits
            // skips rows pointing at a unit that no longer exists, which the FK would reject.
            migrationBuilder.Sql(@"
                INSERT INTO `DiwanBureaus` (`DiwanMailId`, `OrgUnitId`, `CreatedAt`)
                SELECT m.`Id`, m.`ToDiwanOrgUnitId`, UTC_TIMESTAMP()
                FROM `DiwanMails` m
                INNER JOIN `OrgUnits` o ON o.`Id` = m.`ToDiwanOrgUnitId`
                WHERE m.`ToDiwanOrgUnitId` IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "ToDiwanOrgUnitId",
                table: "DiwanMails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ToDiwanOrgUnitId",
                table: "DiwanMails",
                type: "bigint",
                nullable: true);

            // The old column holds one bureau; keep the lowest-id routing per letter. Letters routed
            // to several bureaus lose the extra links — unavoidable when narrowing to one column.
            migrationBuilder.Sql(@"
                UPDATE `DiwanMails` m
                SET m.`ToDiwanOrgUnitId` = (
                    SELECT MIN(b.`OrgUnitId`) FROM `DiwanBureaus` b WHERE b.`DiwanMailId` = m.`Id`
                );");

            migrationBuilder.DropTable(
                name: "DiwanBureaus");
        }
    }
}
