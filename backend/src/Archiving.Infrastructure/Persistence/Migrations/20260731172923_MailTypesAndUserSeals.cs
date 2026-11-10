using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MailTypesAndUserSeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SignatureUrl",
                table: "Users",
                type: "longtext",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StampUrl",
                table: "Users",
                type: "longtext",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "MailCategories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "CategoryId",
                table: "InternalMails",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalMails_CategoryId",
                table: "InternalMails",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_InternalMails_MailCategories_CategoryId",
                table: "InternalMails",
                column: "CategoryId",
                principalTable: "MailCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Existing databases already have mail types, so the service's first-use seed will
            // never fire again for them — without this step the internal register would offer an
            // empty type list forever. Top up the types the internal side needs (and the external
            // financial claim), once per institution, skipping any an admin already created.
            migrationBuilder.Sql(
                """
                INSERT INTO `MailCategories` (`InstitutionId`, `Name`, `Scope`, `SortOrder`, `IsActive`, `CreatedAt`)
                SELECT i.`Id`, d.`Name`, d.`Scope`, d.`SortOrder`, 1, UTC_TIMESTAMP()
                FROM `Institutions` i
                JOIN (
                    SELECT 'مطالبة مالية' AS `Name`, 0 AS `Scope`, 100 AS `SortOrder`
                    UNION ALL SELECT 'قسيمة راتب', 1, 101
                    UNION ALL SELECT 'سند قبض',    1, 102
                    UNION ALL SELECT 'سند دفع',    1, 103
                ) d
                WHERE NOT EXISTS (
                    SELECT 1 FROM `MailCategories` c
                    WHERE c.`InstitutionId` = i.`Id` AND c.`Name` = d.`Name`
                );
                """);

            // These two are meaningful on either register, so they are not left external-only.
            migrationBuilder.Sql(
                "UPDATE `MailCategories` SET `Scope` = 2 WHERE `Name` IN ('عام', 'تعميم');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InternalMails_MailCategories_CategoryId",
                table: "InternalMails");

            migrationBuilder.DropIndex(
                name: "IX_InternalMails_CategoryId",
                table: "InternalMails");

            migrationBuilder.DropColumn(
                name: "SignatureUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "StampUrl",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "MailCategories");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "InternalMails");
        }
    }
}
