using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EntityType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    FieldKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Label = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    FieldType = table.Column<int>(type: "int", nullable: false),
                    Options = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Searchable = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefinitions", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    FieldId = table.Column<long>(type: "bigint", nullable: false),
                    EntityType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "longtext", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldValues", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_EntityType",
                table: "CustomFieldDefinitions",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_EntityType_FieldKey",
                table: "CustomFieldDefinitions",
                columns: new[] { "EntityType", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_EntityType_EntityId",
                table: "CustomFieldValues",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_FieldId_EntityId",
                table: "CustomFieldValues",
                columns: new[] { "FieldId", "EntityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomFieldDefinitions");

            migrationBuilder.DropTable(
                name: "CustomFieldValues");
        }
    }
}
