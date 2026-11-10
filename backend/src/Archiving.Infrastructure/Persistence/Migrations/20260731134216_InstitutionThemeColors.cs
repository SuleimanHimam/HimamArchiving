using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The rail / nav / login theme colours on Institution reached the model snapshot without ever
    /// getting a migration of their own, so the columns were missing from the database and every
    /// branding query failed with "Unknown column 'i.ColorLoginAccent'". This adds them.
    ///
    /// Written by hand: since the snapshot already described the columns, EF diffed the model
    /// against it, found no difference, and generated an empty Up(). The definitions below match
    /// what the snapshot declares for each property — nullable, max length 256.
    /// </summary>
    public partial class InstitutionThemeColors : Migration
    {
        private static readonly string[] Columns =
        [
            "ColorRailBg", "ColorRailText", "ColorRailAccent",
            "ColorNavBg", "ColorNavText",
            "ColorLoginBg", "ColorLoginAccent",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var column in Columns)
            {
                migrationBuilder.AddColumn<string>(
                    name: column,
                    table: "Institutions",
                    type: "varchar(256)",
                    maxLength: 256,
                    nullable: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var column in Columns)
            {
                migrationBuilder.DropColumn(name: column, table: "Institutions");
            }
        }
    }
}
