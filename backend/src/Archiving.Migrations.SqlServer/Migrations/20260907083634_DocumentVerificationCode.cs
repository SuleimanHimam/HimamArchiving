using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class DocumentVerificationCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VerificationCode",
                table: "Documents",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_VerificationCode",
                table: "Documents",
                column: "VerificationCode",
                unique: true,
                filter: "[VerificationCode] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Documents_VerificationCode",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "VerificationCode",
                table: "Documents");
        }
    }
}
