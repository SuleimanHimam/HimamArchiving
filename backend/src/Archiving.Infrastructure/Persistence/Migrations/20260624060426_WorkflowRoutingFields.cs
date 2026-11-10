using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowRoutingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignmentExpression",
                table: "WorkflowStages",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignmentType",
                table: "WorkflowStages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ConditionExpression",
                table: "WorkflowStages",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParallelGroupId",
                table: "WorkflowStages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresAllParallelComplete",
                table: "WorkflowStages",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "WorkflowStages",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignmentExpression",
                table: "WorkflowStages");

            migrationBuilder.DropColumn(
                name: "AssignmentType",
                table: "WorkflowStages");

            migrationBuilder.DropColumn(
                name: "ConditionExpression",
                table: "WorkflowStages");

            migrationBuilder.DropColumn(
                name: "ParallelGroupId",
                table: "WorkflowStages");

            migrationBuilder.DropColumn(
                name: "RequiresAllParallelComplete",
                table: "WorkflowStages");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "WorkflowStages");
        }
    }
}
