using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ArchivedObjectEncryptionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArchivedObjects",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StorageKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Algorithm = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    KekId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    WrappedDek = table.Column<byte[]>(type: "varbinary(1024)", maxLength: 1024, nullable: true),
                    NoncePrefix = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: true),
                    ChunkSize = table.Column<int>(type: "int", nullable: false),
                    ShreddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShreddedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastRewrappedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedObjects", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedObjects_KekId",
                table: "ArchivedObjects",
                column: "KekId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedObjects_ShreddedAtUtc",
                table: "ArchivedObjects",
                column: "ShreddedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedObjects_StorageKey",
                table: "ArchivedObjects",
                column: "StorageKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivedObjects");
        }
    }
}
