using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// قسم الأرشيف is the archive staff's workspace, so the built-in Employee and Manager roles no
    /// longer receive <c>Archive.*</c> (see PermissionCatalog). Startup seeding only ever *adds*
    /// grants — deliberately, so it cannot undo an administrator's work — which means roles seeded
    /// before this change would keep the grant forever. This removes it once.
    ///
    /// Scoped to the two built-in roles by name: a custom role, or an Archive.View an administrator
    /// granted deliberately elsewhere, is untouched, and nothing re-applies after this runs.
    /// </summary>
    public partial class RevokeArchiveFromNonArchiveRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE rp FROM `RolePermissions` rp
                JOIN `Roles` r       ON r.`Id` = rp.`RoleId`
                JOIN `Permissions` p ON p.`Id` = rp.`PermissionId`
                WHERE r.`IsSystem` = 1
                  AND r.`Name` IN ('Employee', 'Manager')
                  AND p.`Code` LIKE 'Archive.%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reinstated: which of these grants were deliberate is not recoverable from here.
            // Re-grant from the roles screen if a role is meant to reach the physical archive.
        }
    }
}
