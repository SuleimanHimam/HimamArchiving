using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCorrespondenceMailDiwan : Migration
    {
        // Drops the correspondence-mail domain that was removed from the model:
        // incoming/outgoing mail, mail attachments, letter templates, the correspondence
        // memo thread + its recipients, and the diwan registry — plus the mail-link columns
        // on Document and PhysicalArchiveItem.
        //
        // CorrespondenceGroup / CorrespondenceGroupMember are intentionally KEPT: they back
        // the distribution-groups feature (GroupsController / lib/groups), not correspondence-mail.
        //
        // Hand-authored: the model snapshot had already advanced to the post-removal state during
        // scaffolding, so EF could not diff the drops. The operations below match the DB's actual
        // pre-removal schema (tables created by the earlier _Correspondence and _DiwanRegistry
        // migrations). The FK/table order is dependency-safe.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PhysicalArchiveItems_IncomingMails_IncomingMailId",
                table: "PhysicalArchiveItems");

            migrationBuilder.DropTable(name: "CorrespondenceRecipients");
            migrationBuilder.DropTable(name: "Diwans");
            migrationBuilder.DropTable(name: "MailAttachments");
            migrationBuilder.DropTable(name: "Correspondences");
            migrationBuilder.DropTable(name: "OutgoingMails");
            migrationBuilder.DropTable(name: "IncomingMails");
            migrationBuilder.DropTable(name: "LetterTemplates");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalArchiveItems_IncomingMailId",
                table: "PhysicalArchiveItems");

            migrationBuilder.DropColumn(name: "IncomingMailId", table: "PhysicalArchiveItems");
            migrationBuilder.DropColumn(name: "LinkedMailDirection", table: "Documents");
            migrationBuilder.DropColumn(name: "LinkedMailId", table: "Documents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Irreversible by design: the correspondence-mail domain (entities, services,
            // controllers, DTOs) was deleted from the codebase, so these tables cannot be
            // reconstructed from the model. Restore from source control + a DB backup if needed.
            throw new NotSupportedException(
                "RemoveCorrespondenceMailDiwan is not reversible: the correspondence-mail domain was removed from the codebase.");
        }
    }
}
