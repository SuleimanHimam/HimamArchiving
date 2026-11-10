using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CorrespondenceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CorrespondenceTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    NameAr = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceTypes", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Correspondences",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    CorrespondenceNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    TypeId = table.Column<long>(type: "bigint", nullable: true),
                    Subject = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Keywords = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    CorrespondenceDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResponseDeadline = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SenderKind = table.Column<int>(type: "int", nullable: false),
                    SenderId = table.Column<long>(type: "bigint", nullable: true),
                    SenderName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: true),
                    ResponsePeriodDays = table.Column<int>(type: "int", nullable: true),
                    RemindOnNoReply = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NoReplyNotifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AddresseeName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    AddresseeTitle = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    FileNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    RecordSeriesId = table.Column<long>(type: "bigint", nullable: true),
                    IsArchived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReplyToId = table.Column<long>(type: "bigint", nullable: true),
                    ParentCorrespondenceId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Correspondences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Correspondences_CorrespondenceTypes_TypeId",
                        column: x => x.TypeId,
                        principalTable: "CorrespondenceTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Correspondences_ParentCorrespondenceId",
                        column: x => x.ParentCorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Correspondences_Correspondences_ReplyToId",
                        column: x => x.ReplyToId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CorrespondenceAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CorrespondenceId = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Checksum = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    FileName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    OcrText = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    OcrStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceAttachments_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CorrespondenceLinks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    TargetId = table.Column<long>(type: "bigint", nullable: false),
                    LinkType = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceLinks_Correspondences_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CorrespondenceLinks_Correspondences_TargetId",
                        column: x => x.TargetId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CorrespondenceReads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CorrespondenceId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ReadStatus = table.Column<int>(type: "int", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FirstReadAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastReadAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceReads_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CorrespondenceRecipients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CorrespondenceId = table.Column<long>(type: "bigint", nullable: false),
                    Addressing = table.Column<int>(type: "int", nullable: false),
                    TargetKind = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<long>(type: "bigint", nullable: true),
                    TargetName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceRecipients_Correspondences_CorrespondenceId",
                        column: x => x.CorrespondenceId,
                        principalTable: "Correspondences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceAttachments_CorrespondenceId",
                table: "CorrespondenceAttachments",
                column: "CorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceLinks_SourceId_TargetId_LinkType",
                table: "CorrespondenceLinks",
                columns: new[] { "SourceId", "TargetId", "LinkType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceLinks_TargetId",
                table: "CorrespondenceLinks",
                column: "TargetId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReads_CorrespondenceId_UserId",
                table: "CorrespondenceReads",
                columns: new[] { "CorrespondenceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceReads_UserId_ReadStatus",
                table: "CorrespondenceReads",
                columns: new[] { "UserId", "ReadStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceRecipients_CorrespondenceId_Addressing",
                table: "CorrespondenceRecipients",
                columns: new[] { "CorrespondenceId", "Addressing" });

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_InstitutionId_Classification",
                table: "Correspondences",
                columns: new[] { "InstitutionId", "Classification" });

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_InstitutionId_CorrespondenceDate",
                table: "Correspondences",
                columns: new[] { "InstitutionId", "CorrespondenceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_InstitutionId_CorrespondenceNumber",
                table: "Correspondences",
                columns: new[] { "InstitutionId", "CorrespondenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_InstitutionId_Direction_Status",
                table: "Correspondences",
                columns: new[] { "InstitutionId", "Direction", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_ParentCorrespondenceId",
                table: "Correspondences",
                column: "ParentCorrespondenceId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_RemindOnNoReply_ResponseDeadline",
                table: "Correspondences",
                columns: new[] { "RemindOnNoReply", "ResponseDeadline" });

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_ReplyToId",
                table: "Correspondences",
                column: "ReplyToId");

            migrationBuilder.CreateIndex(
                name: "IX_Correspondences_TypeId",
                table: "Correspondences",
                column: "TypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceTypes_InstitutionId_IsActive",
                table: "CorrespondenceTypes",
                columns: new[] { "InstitutionId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CorrespondenceAttachments");

            migrationBuilder.DropTable(
                name: "CorrespondenceLinks");

            migrationBuilder.DropTable(
                name: "CorrespondenceReads");

            migrationBuilder.DropTable(
                name: "CorrespondenceRecipients");

            migrationBuilder.DropTable(
                name: "Correspondences");

            migrationBuilder.DropTable(
                name: "CorrespondenceTypes");
        }
    }
}
