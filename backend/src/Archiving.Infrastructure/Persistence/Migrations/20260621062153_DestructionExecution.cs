using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DestructionExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ContentDestroyed",
                table: "DocumentAttachments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContentDestroyedAt",
                table: "DocumentAttachments",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentDestroyed",
                table: "DocumentAttachments");

            migrationBuilder.DropColumn(
                name: "ContentDestroyedAt",
                table: "DocumentAttachments");
        }
    }
}
