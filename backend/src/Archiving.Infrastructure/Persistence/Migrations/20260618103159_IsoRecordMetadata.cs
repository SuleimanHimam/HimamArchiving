using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IsoRecordMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecordAgents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    RecordType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    RecordId = table.Column<long>(type: "bigint", nullable: false),
                    AgentKind = table.Column<int>(type: "int", nullable: false),
                    AgentId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordAgents", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RecordRelationships",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SourceType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    TargetType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    TargetId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordRelationships", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RecordAgents_RecordType_RecordId",
                table: "RecordAgents",
                columns: new[] { "RecordType", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordAgents_RecordType_RecordId_AgentKind_AgentId_Role",
                table: "RecordAgents",
                columns: new[] { "RecordType", "RecordId", "AgentKind", "AgentId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordRelationships_SourceType_SourceId",
                table: "RecordRelationships",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordRelationships_SourceType_SourceId_TargetType_TargetId_~",
                table: "RecordRelationships",
                columns: new[] { "SourceType", "SourceId", "TargetType", "TargetId", "Type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecordAgents");

            migrationBuilder.DropTable(
                name: "RecordRelationships");
        }
    }
}
