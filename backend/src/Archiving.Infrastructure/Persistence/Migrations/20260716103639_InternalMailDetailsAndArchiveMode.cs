using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InternalMailDetailsAndArchiveMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Classification",
                table: "OutgoingMails",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileReference",
                table: "OutgoingMails",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Keywords",
                table: "OutgoingMails",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RaisedTo",
                table: "OutgoingMails",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplyByDate",
                table: "OutgoingMails",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoutedOrgUnitIds",
                table: "OutgoingMails",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Classification",
                table: "IncomingMails",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileReference",
                table: "IncomingMails",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RaisedTo",
                table: "IncomingMails",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReplyByDate",
                table: "IncomingMails",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoutedOrgUnitIds",
                table: "IncomingMails",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "IncomingMails",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArchiveMode",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LinkedMailDirection",
                table: "Documents",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Classification",
                table: "OutgoingMails");

            migrationBuilder.DropColumn(
                name: "FileReference",
                table: "OutgoingMails");

            migrationBuilder.DropColumn(
                name: "Keywords",
                table: "OutgoingMails");

            migrationBuilder.DropColumn(
                name: "RaisedTo",
                table: "OutgoingMails");

            migrationBuilder.DropColumn(
                name: "ReplyByDate",
                table: "OutgoingMails");

            migrationBuilder.DropColumn(
                name: "RoutedOrgUnitIds",
                table: "OutgoingMails");

            migrationBuilder.DropColumn(
                name: "Classification",
                table: "IncomingMails");

            migrationBuilder.DropColumn(
                name: "FileReference",
                table: "IncomingMails");

            migrationBuilder.DropColumn(
                name: "RaisedTo",
                table: "IncomingMails");

            migrationBuilder.DropColumn(
                name: "ReplyByDate",
                table: "IncomingMails");

            migrationBuilder.DropColumn(
                name: "RoutedOrgUnitIds",
                table: "IncomingMails");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "IncomingMails");

            migrationBuilder.DropColumn(
                name: "ArchiveMode",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "LinkedMailDirection",
                table: "Documents");
        }
    }
}
