using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTasksUiRecordsArchiveVault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ArchiveFolders was created by a parallel-branch migration absent from this tree, so it
            // may not exist in every database — drop it defensively.
            migrationBuilder.Sql("DROP TABLE IF EXISTS `ArchiveFolders`;");

            migrationBuilder.DropTable(
                name: "RecordSeries");

            migrationBuilder.DropTable(
                name: "FilePlanNodes");

            migrationBuilder.DropIndex(
                name: "IX_Documents_RecordSeriesId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FilePlanNodeId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RecordSeriesId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "RecordSeriesId",
                table: "Correspondences");

            migrationBuilder.DropColumn(
                name: "RecordSeriesId",
                table: "AdministrativeDecisions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FilePlanNodeId",
                table: "Documents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RecordSeriesId",
                table: "Documents",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RecordSeriesId",
                table: "Correspondences",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RecordSeriesId",
                table: "AdministrativeDecisions",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArchiveFolders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchiveFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchiveFolders_ArchiveFolders_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ArchiveFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "FilePlanNodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilePlanNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FilePlanNodes_FilePlanNodes_ParentId",
                        column: x => x.ParentId,
                        principalTable: "FilePlanNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RecordSeries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    FilePlanNodeId = table.Column<long>(type: "bigint", nullable: true),
                    Code = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    DispositionDefault = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsPermanent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    RetentionMonths = table.Column<int>(type: "int", nullable: false),
                    RetentionTrigger = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecordSeries_FilePlanNodes_FilePlanNodeId",
                        column: x => x.FilePlanNodeId,
                        principalTable: "FilePlanNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_RecordSeriesId",
                table: "Documents",
                column: "RecordSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveFolders_InstitutionId_ParentId",
                table: "ArchiveFolders",
                columns: new[] { "InstitutionId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArchiveFolders_ParentId",
                table: "ArchiveFolders",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_FilePlanNodes_Code",
                table: "FilePlanNodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FilePlanNodes_ParentId",
                table: "FilePlanNodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_RecordSeries_Code",
                table: "RecordSeries",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordSeries_FilePlanNodeId",
                table: "RecordSeries",
                column: "FilePlanNodeId");
        }
    }
}
