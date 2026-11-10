using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DiwanRegisterFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CategoryId",
                table: "DiwanMails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IssueDate",
                table: "DiwanMails",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReferenceToId",
                table: "DiwanMails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentDate",
                table: "DiwanMails",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ToDiwanOrgUnitId",
                table: "DiwanMails",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MailCategories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailCategories", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DiwanMails_CategoryId",
                table: "DiwanMails",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MailCategories_InstitutionId_IsActive",
                table: "MailCategories",
                columns: new[] { "InstitutionId", "IsActive" });

            migrationBuilder.AddForeignKey(
                name: "FK_DiwanMails_MailCategories_CategoryId",
                table: "DiwanMails",
                column: "CategoryId",
                principalTable: "MailCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiwanMails_MailCategories_CategoryId",
                table: "DiwanMails");

            migrationBuilder.DropTable(
                name: "MailCategories");

            migrationBuilder.DropIndex(
                name: "IX_DiwanMails_CategoryId",
                table: "DiwanMails");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "DiwanMails");

            migrationBuilder.DropColumn(
                name: "IssueDate",
                table: "DiwanMails");

            migrationBuilder.DropColumn(
                name: "ReferenceToId",
                table: "DiwanMails");

            migrationBuilder.DropColumn(
                name: "SentDate",
                table: "DiwanMails");

            migrationBuilder.DropColumn(
                name: "ToDiwanOrgUnitId",
                table: "DiwanMails");
        }
    }
}
