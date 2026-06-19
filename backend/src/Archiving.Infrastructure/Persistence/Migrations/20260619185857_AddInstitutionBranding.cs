using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstitutionBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorAccent",
                table: "Institutions",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorPrimary",
                table: "Institutions",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LogoBase64",
                table: "Institutions",
                type: "longtext",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorAccent",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "ColorPrimary",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "LogoBase64",
                table: "Institutions");
        }
    }
}
