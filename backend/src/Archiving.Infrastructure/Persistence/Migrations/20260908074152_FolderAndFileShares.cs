using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FolderAndFileShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FolderShares",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    FolderId = table.Column<long>(type: "bigint", nullable: false),
                    SharedWithUserId = table.Column<long>(type: "bigint", nullable: false),
                    SharedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CanEdit = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FolderShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FolderShares_Folders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "Folders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "UserFileShares",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserFileId = table.Column<long>(type: "bigint", nullable: false),
                    SharedWithUserId = table.Column<long>(type: "bigint", nullable: false),
                    SharedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CanEdit = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFileShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFileShares_UserFiles_UserFileId",
                        column: x => x.UserFileId,
                        principalTable: "UserFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_FolderShares_FolderId_SharedWithUserId",
                table: "FolderShares",
                columns: new[] { "FolderId", "SharedWithUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FolderShares_SharedWithUserId",
                table: "FolderShares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFileShares_SharedWithUserId",
                table: "UserFileShares",
                column: "SharedWithUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserFileShares_UserFileId_SharedWithUserId",
                table: "UserFileShares",
                columns: new[] { "UserFileId", "SharedWithUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FolderShares");

            migrationBuilder.DropTable(
                name: "UserFileShares");
        }
    }
}
