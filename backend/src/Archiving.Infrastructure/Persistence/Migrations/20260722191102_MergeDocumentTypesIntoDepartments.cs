using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeDocumentTypesIntoDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: the scaffolder put DropTable first, which would throw the catalogue away.
            // Columns are added and the rows carried over BEFORE the old table is dropped.
            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "DocumentTypes",
                type: "longtext",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Number",
                table: "DocumentTypes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OrgUnitId",
                table: "DocumentTypes",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_OrgUnitId",
                table: "DocumentTypes",
                column: "OrgUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentTypes_OrgUnits_OrgUnitId",
                table: "DocumentTypes",
                column: "OrgUnitId",
                principalTable: "OrgUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Carry every per-department attachment type over as a department-owned document
            // type. Retention was kept in years there and in months here. The INNER JOIN skips
            // rows whose department no longer exists, which the restrict FK would reject.
            // AllowedUploadSources 15 = UploadSource.All.
            migrationBuilder.Sql(@"
                INSERT INTO DocumentTypes
                    (Name, DefaultConfidentiality, RetentionMonths, RequiresApproval,
                     RequiresPhysicalArchiving, AllowedUploadSources, IsActive,
                     OrgUnitId, Number, Note, CreatedAt, CreatedBy)
                SELECT d.Name, d.Confidentiality, COALESCE(d.RetentionYears, 10) * 12,
                       d.RequiresApprovalDocument, 0, 15, d.IsActive,
                       d.OrgUnitId, d.Number, d.Note, d.CreatedAt, d.CreatedBy
                FROM DepartmentAttachmentTypes d
                INNER JOIN OrgUnits o ON o.Id = d.OrgUnitId;");

            migrationBuilder.DropTable(
                name: "DepartmentAttachmentTypes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Recreate the table and move the department-owned rows back BEFORE the columns
            // that hold them are dropped (see RestoreCatalogue below).
            migrationBuilder.CreateTable(
                name: "DepartmentAttachmentTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: false),
                    Confidentiality = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Note = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Number = table.Column<int>(type: "int", nullable: false),
                    RequiresApprovalDocument = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RetentionYears = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentAttachmentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DepartmentAttachmentTypes_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentAttachmentTypes_OrgUnitId",
                table: "DepartmentAttachmentTypes",
                column: "OrgUnitId");

            // Move the department-owned types back, rounding retention months up to whole years.
            // Types still referenced by a document are left in place — deleting them would break
            // Documents.DocumentTypeId — so only unused ones are moved out.
            migrationBuilder.Sql(@"
                INSERT INTO DepartmentAttachmentTypes
                    (OrgUnitId, Number, Name, Confidentiality, RetentionYears, Note,
                     RequiresApprovalDocument, IsActive, CreatedAt, CreatedBy)
                SELECT t.OrgUnitId, COALESCE(t.Number, 0), t.Name, t.DefaultConfidentiality,
                       CEIL(t.RetentionMonths / 12), t.Note, t.RequiresApproval, t.IsActive,
                       t.CreatedAt, t.CreatedBy
                FROM DocumentTypes t
                WHERE t.OrgUnitId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM Documents d WHERE d.DocumentTypeId = t.Id);");

            migrationBuilder.Sql(@"
                DELETE t FROM DocumentTypes t
                WHERE t.OrgUnitId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM Documents d WHERE d.DocumentTypeId = t.Id);");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentTypes_OrgUnits_OrgUnitId",
                table: "DocumentTypes");

            migrationBuilder.DropIndex(
                name: "IX_DocumentTypes_OrgUnitId",
                table: "DocumentTypes");

            migrationBuilder.DropColumn(name: "Note", table: "DocumentTypes");
            migrationBuilder.DropColumn(name: "Number", table: "DocumentTypes");
            migrationBuilder.DropColumn(name: "OrgUnitId", table: "DocumentTypes");
        }
    }
}
