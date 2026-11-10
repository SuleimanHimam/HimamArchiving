using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkflowInternalMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "InternalMailId",
                table: "WorkflowTasks",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinimumApprovals",
                table: "WorkflowStages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RejectBehavior",
                table: "WorkflowStages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReturnResume",
                table: "WorkflowStages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "ChangesRequestedByStageId",
                table: "WorkflowInstances",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RootInternalMailId",
                table: "WorkflowInstances",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "MailCategoryId",
                table: "WorkflowDefinitions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WorkflowInstanceId",
                table: "InternalMails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkflowRole",
                table: "InternalMails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "WorkflowTaskId",
                table: "InternalMails",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTasks_InternalMailId",
                table: "WorkflowTasks",
                column: "InternalMailId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_RootInternalMailId",
                table: "WorkflowInstances",
                column: "RootInternalMailId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_TriggerModule_MailCategoryId_IsActive",
                table: "WorkflowDefinitions",
                columns: new[] { "TriggerModule", "MailCategoryId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalMails_WorkflowInstanceId",
                table: "InternalMails",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalMails_WorkflowTaskId",
                table: "InternalMails",
                column: "WorkflowTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkflowTasks_InternalMailId",
                table: "WorkflowTasks");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowInstances_RootInternalMailId",
                table: "WorkflowInstances");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowDefinitions_TriggerModule_MailCategoryId_IsActive",
                table: "WorkflowDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_InternalMails_WorkflowInstanceId",
                table: "InternalMails");

            migrationBuilder.DropIndex(
                name: "IX_InternalMails_WorkflowTaskId",
                table: "InternalMails");

            migrationBuilder.DropColumn(
                name: "InternalMailId",
                table: "WorkflowTasks");

            migrationBuilder.DropColumn(
                name: "MinimumApprovals",
                table: "WorkflowStages");

            migrationBuilder.DropColumn(
                name: "RejectBehavior",
                table: "WorkflowStages");

            migrationBuilder.DropColumn(
                name: "ReturnResume",
                table: "WorkflowStages");

            migrationBuilder.DropColumn(
                name: "ChangesRequestedByStageId",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "RootInternalMailId",
                table: "WorkflowInstances");

            migrationBuilder.DropColumn(
                name: "MailCategoryId",
                table: "WorkflowDefinitions");

            migrationBuilder.DropColumn(
                name: "WorkflowInstanceId",
                table: "InternalMails");

            migrationBuilder.DropColumn(
                name: "WorkflowRole",
                table: "InternalMails");

            migrationBuilder.DropColumn(
                name: "WorkflowTaskId",
                table: "InternalMails");
        }
    }
}
