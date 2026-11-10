using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
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
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StorageKey = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ChecksumSha256 = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    Algorithm = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                    KekId = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    WrappedDek = table.Column<byte[]>(type: "varbinary(1024)", maxLength: 1024, nullable: true),
                    NoncePrefix = table.Column<byte[]>(type: "varbinary(16)", maxLength: 16, nullable: true),
                    ChunkSize = table.Column<int>(type: "int", nullable: false),
                    ShreddedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ShreddedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    LastRewrappedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedObjects", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

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
