using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MailInternalRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "GeneratedIncomingMailId",
                table: "OutgoingMails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RecipientOrgUnitId",
                table: "OutgoingMails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecipientType",
                table: "OutgoingMails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "RecipientUserId",
                table: "OutgoingMails",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "SourceOutgoingMailId",
                table: "IncomingMails",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeneratedIncomingMailId",
                table: "OutgoingMails");

            migrationBuilder.DropColumn(
                name: "RecipientOrgUnitId",
                table: "OutgoingMails");

            migrationBuilder.DropColumn(
                name: "RecipientType",
                table: "OutgoingMails");

            migrationBuilder.DropColumn(
                name: "RecipientUserId",
                table: "OutgoingMails");

            migrationBuilder.DropColumn(
                name: "SourceOutgoingMailId",
                table: "IncomingMails");
        }
    }
}
