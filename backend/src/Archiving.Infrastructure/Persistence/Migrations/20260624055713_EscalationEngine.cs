using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EscalationEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueEscalatedAtUtc",
                table: "CorrespondenceRecipients",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EscalationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: false),
                    WorkflowStageId = table.Column<long>(type: "bigint", nullable: false),
                    EscalationRuleId = table.Column<long>(type: "bigint", nullable: false),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    EscalatedToPositionId = table.Column<long>(type: "bigint", nullable: false),
                    ActionTaken = table.Column<int>(type: "int", nullable: false),
                    EscalatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationLogs", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EscalationRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    WorkflowDefinitionId = table.Column<long>(type: "bigint", nullable: true),
                    WorkflowStageId = table.Column<long>(type: "bigint", nullable: true),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    OverdueMinutes = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    EscalateToPositionId = table.Column<long>(type: "bigint", nullable: true),
                    NotifyOriginator = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationRules", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationLogs_WorkflowInstanceId_WorkflowStageId_Escalation~",
                table: "EscalationLogs",
                columns: new[] { "WorkflowInstanceId", "WorkflowStageId", "EscalationLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_WorkflowDefinitionId_WorkflowStageId_IsActive",
                table: "EscalationRules",
                columns: new[] { "WorkflowDefinitionId", "WorkflowStageId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EscalationLogs");

            migrationBuilder.DropTable(
                name: "EscalationRules");

            migrationBuilder.DropColumn(
                name: "OverdueEscalatedAtUtc",
                table: "CorrespondenceRecipients");
        }
    }
}
