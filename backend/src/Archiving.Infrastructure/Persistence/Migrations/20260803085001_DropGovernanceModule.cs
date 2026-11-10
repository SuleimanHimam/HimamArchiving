using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <summary>Drops the الاجتماعات والقرارات (committees / meetings / decisions) module, removed
    /// from the product. Deletes its rows with it — <c>Down</c> restores the schema but not the data.</summary>
    public partial class DropGovernanceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministrativeDecisions");

            migrationBuilder.DropTable(
                name: "AgendaItems");

            migrationBuilder.DropTable(
                name: "CommitteeMembers");

            migrationBuilder.DropTable(
                name: "MeetingAttendees");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropTable(
                name: "Committees");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Committees",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Committees", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CommitteeMembers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CommitteeId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    PositionId = table.Column<long>(type: "bigint", nullable: true),
                    Role = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    TermEnd = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TermStart = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommitteeMembers_Committees_CommitteeId",
                        column: x => x.CommitteeId,
                        principalTable: "Committees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CommitteeId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    Location = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    MeetingDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Notes = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meetings_Committees_CommitteeId",
                        column: x => x.CommitteeId,
                        principalTable: "Committees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AdministrativeDecisions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    IssuingCommitteeId = table.Column<long>(type: "bigint", nullable: true),
                    MeetingId = table.Column<long>(type: "bigint", nullable: true),
                    Body = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Confidentiality = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    DecisionNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsPermanent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IssuingOrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministrativeDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdministrativeDecisions_Committees_IssuingCommitteeId",
                        column: x => x.IssuingCommitteeId,
                        principalTable: "Committees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AdministrativeDecisions_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AgendaItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    MeetingId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    DecisionId = table.Column<long>(type: "bigint", nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgendaItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgendaItems_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "MeetingAttendees",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    MeetingId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Present = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MeetingAttendees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MeetingAttendees_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeDecisions_DecisionNumber",
                table: "AdministrativeDecisions",
                column: "DecisionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeDecisions_IssuingCommitteeId",
                table: "AdministrativeDecisions",
                column: "IssuingCommitteeId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeDecisions_MeetingId",
                table: "AdministrativeDecisions",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeDecisions_Status",
                table: "AdministrativeDecisions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaItems_MeetingId",
                table: "AgendaItems",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeMembers_CommitteeId",
                table: "CommitteeMembers",
                column: "CommitteeId");

            migrationBuilder.CreateIndex(
                name: "IX_Committees_Name",
                table: "Committees",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_MeetingAttendees_MeetingId",
                table: "MeetingAttendees",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_CommitteeId",
                table: "Meetings",
                column: "CommitteeId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_MeetingDate",
                table: "Meetings",
                column: "MeetingDate");
        }
    }
}
