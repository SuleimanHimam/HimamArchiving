using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowStageColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "WorkflowStages",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "WorkflowStages");
        }
    }
}
