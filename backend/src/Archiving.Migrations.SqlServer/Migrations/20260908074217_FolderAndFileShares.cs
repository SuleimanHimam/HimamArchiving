using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Migrations.SqlServer.Migrations
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
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FolderId = table.Column<long>(type: "bigint", nullable: false),
                    SharedWithUserId = table.Column<long>(type: "bigint", nullable: false),
                    SharedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                });

            migrationBuilder.CreateTable(
                name: "UserFileShares",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserFileId = table.Column<long>(type: "bigint", nullable: false),
                    SharedWithUserId = table.Column<long>(type: "bigint", nullable: false),
                    SharedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                });

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
