using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NumberingTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NumberingTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EntityType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: true),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentTypeId = table.Column<long>(type: "bigint", nullable: true),
                    Pattern = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    ResetFrequency = table.Column<int>(type: "int", nullable: false),
                    CurrentSequence = table.Column<int>(type: "int", nullable: false),
                    CurrentYear = table.Column<int>(type: "int", nullable: true),
                    CurrentMonth = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberingTemplates", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_NumberingTemplates_EntityType_InstitutionId_OrgUnitId_Docume~",
                table: "NumberingTemplates",
                columns: new[] { "EntityType", "InstitutionId", "OrgUnitId", "DocumentTypeId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NumberingTemplates");
        }
    }
}
