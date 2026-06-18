using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixNotificationRecipientFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_RecipientId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Positions_Users_CurrentOccupantId",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_Positions_CurrentOccupantId",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_RecipientId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "CurrentOccupantId",
                table: "Positions");

            migrationBuilder.DropColumn(
                name: "RecipientId",
                table: "Notifications");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_CurrentOccupantUserId",
                table: "Positions",
                column: "CurrentOccupantUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_RecipientUserId",
                table: "Notifications",
                column: "RecipientUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Positions_Users_CurrentOccupantUserId",
                table: "Positions",
                column: "CurrentOccupantUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_RecipientUserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Positions_Users_CurrentOccupantUserId",
                table: "Positions");

            migrationBuilder.DropIndex(
                name: "IX_Positions_CurrentOccupantUserId",
                table: "Positions");

            migrationBuilder.AddColumn<long>(
                name: "CurrentOccupantId",
                table: "Positions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RecipientId",
                table: "Notifications",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Positions_CurrentOccupantId",
                table: "Positions",
                column: "CurrentOccupantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId",
                table: "Notifications",
                column: "RecipientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_RecipientId",
                table: "Notifications",
                column: "RecipientId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Positions_Users_CurrentOccupantId",
                table: "Positions",
                column: "CurrentOccupantId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
