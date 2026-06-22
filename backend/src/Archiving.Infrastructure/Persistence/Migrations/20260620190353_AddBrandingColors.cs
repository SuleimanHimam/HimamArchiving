using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBrandingColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ColorBg",
                table: "Institutions",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorSeal",
                table: "Institutions",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColorBg",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "ColorSeal",
                table: "Institutions");
        }
    }
}
