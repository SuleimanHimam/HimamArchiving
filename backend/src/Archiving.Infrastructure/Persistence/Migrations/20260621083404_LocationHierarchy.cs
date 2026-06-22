using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocationHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "BoxId",
                table: "Documents",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    NameAr = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Code = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Address = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    BuildingId = table.Column<long>(type: "bigint", nullable: false),
                    NameAr = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    RoomNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Floor = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Cabinets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    RoomId = table.Column<long>(type: "bigint", nullable: false),
                    NameAr = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    CabinetCode = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    ShelfCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cabinets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cabinets_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RoomConnections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    RoomId = table.Column<long>(type: "bigint", nullable: false),
                    ConnectedRoomId = table.Column<long>(type: "bigint", nullable: false),
                    ConnectionType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomConnections_Rooms_ConnectedRoomId",
                        column: x => x.ConnectedRoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomConnections_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Shelves",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CabinetId = table.Column<long>(type: "bigint", nullable: false),
                    ShelfNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shelves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shelves_Cabinets_CabinetId",
                        column: x => x.CabinetId,
                        principalTable: "Cabinets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Boxes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ShelfId = table.Column<long>(type: "bigint", nullable: true),
                    RoomId = table.Column<long>(type: "bigint", nullable: true),
                    BoxCode = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Barcode = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    CurrentCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Boxes_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Boxes_Shelves_ShelfId",
                        column: x => x.ShelfId,
                        principalTable: "Shelves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_BoxId",
                table: "Documents",
                column: "BoxId");

            migrationBuilder.CreateIndex(
                name: "IX_Boxes_BoxCode",
                table: "Boxes",
                column: "BoxCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Boxes_RoomId",
                table: "Boxes",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Boxes_ShelfId",
                table: "Boxes",
                column: "ShelfId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_Code",
                table: "Buildings",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cabinets_RoomId",
                table: "Cabinets",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomConnections_ConnectedRoomId",
                table: "RoomConnections",
                column: "ConnectedRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomConnections_RoomId_ConnectedRoomId",
                table: "RoomConnections",
                columns: new[] { "RoomId", "ConnectedRoomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_BuildingId",
                table: "Rooms",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Shelves_CabinetId",
                table: "Shelves",
                column: "CabinetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Boxes");

            migrationBuilder.DropTable(
                name: "RoomConnections");

            migrationBuilder.DropTable(
                name: "Shelves");

            migrationBuilder.DropTable(
                name: "Cabinets");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Buildings");

            migrationBuilder.DropIndex(
                name: "IX_Documents_BoxId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "BoxId",
                table: "Documents");
        }
    }
}
