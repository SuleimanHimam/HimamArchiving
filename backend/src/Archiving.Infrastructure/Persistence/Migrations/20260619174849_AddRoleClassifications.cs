using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleClassifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoleClassifications",
                columns: table => new
                {
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ClassificationTypeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClassifications", x => new { x.RoleId, x.ClassificationTypeId });
                    table.ForeignKey(
                        name: "FK_RoleClassifications_ClassificationTypes_ClassificationTypeId",
                        column: x => x.ClassificationTypeId,
                        principalTable: "ClassificationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleClassifications_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClassifications_ClassificationTypeId",
                table: "RoleClassifications",
                column: "ClassificationTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoleClassifications");
        }
    }
}
