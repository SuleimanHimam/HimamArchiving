using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkDiwanToExternalOrg : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DiwanMails_ExternalOrgId",
                table: "DiwanMails",
                column: "ExternalOrgId");

            migrationBuilder.AddForeignKey(
                name: "FK_DiwanMails_ExternalOrganizations_ExternalOrgId",
                table: "DiwanMails",
                column: "ExternalOrgId",
                principalTable: "ExternalOrganizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiwanMails_ExternalOrganizations_ExternalOrgId",
                table: "DiwanMails");

            migrationBuilder.DropIndex(
                name: "IX_DiwanMails_ExternalOrgId",
                table: "DiwanMails");
        }
    }
}
