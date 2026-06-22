using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DestructionCustomMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomMethod",
                table: "DestructionItems",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomMethod",
                table: "DestructionItems");
        }
    }
}
