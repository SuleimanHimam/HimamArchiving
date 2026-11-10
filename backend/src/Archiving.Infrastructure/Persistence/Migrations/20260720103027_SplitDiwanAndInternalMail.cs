using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitDiwanAndInternalMail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateTable(
                name: "DiwanMails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    RegisterNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    ExternalOrgId = table.Column<long>(type: "bigint", nullable: true),
                    ExternalOrgName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    AddresseeName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    AddresseeTitle = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Subject = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Keywords = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    MailDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResponseDeadline = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResponsePeriodDays = table.Column<int>(type: "int", nullable: true),
                    RemindOnNoReply = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NoReplyNotifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: true),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FileNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    IsArchived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ArchivedDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    ReplyToId = table.Column<long>(type: "bigint", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_DiwanMails", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InternalMails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    MailNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    SenderUserId = table.Column<long>(type: "bigint", nullable: false),
                    SenderName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    SenderOrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    Subject = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Keywords = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    MailDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResponseDeadline = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResponsePeriodDays = table.Column<int>(type: "int", nullable: true),
                    RemindOnNoReply = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NoReplyNotifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: true),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FileNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    IsArchived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ArchivedDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    ReplyToId = table.Column<long>(type: "bigint", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_InternalMails", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DiwanAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DiwanMailId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_DiwanAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiwanAttachments_DiwanMails_DiwanMailId",
                        column: x => x.DiwanMailId,
                        principalTable: "DiwanMails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InternalAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InternalMailId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_InternalAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalAttachments_InternalMails_InternalMailId",
                        column: x => x.InternalMailId,
                        principalTable: "InternalMails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InternalReads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InternalMailId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_InternalReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalReads_InternalMails_InternalMailId",
                        column: x => x.InternalMailId,
                        principalTable: "InternalMails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InternalRecipients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InternalMailId = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_InternalRecipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalRecipients_InternalMails_InternalMailId",
                        column: x => x.InternalMailId,
                        principalTable: "InternalMails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DiwanAttachments_DiwanMailId",
                table: "DiwanAttachments",
                column: "DiwanMailId");

            migrationBuilder.CreateIndex(
                name: "IX_DiwanMails_InstitutionId_Direction_Status",
                table: "DiwanMails",
                columns: new[] { "InstitutionId", "Direction", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DiwanMails_InstitutionId_MailDate",
                table: "DiwanMails",
                columns: new[] { "InstitutionId", "MailDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DiwanMails_InstitutionId_RegisterNumber",
                table: "DiwanMails",
                columns: new[] { "InstitutionId", "RegisterNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiwanMails_RemindOnNoReply_ResponseDeadline",
                table: "DiwanMails",
                columns: new[] { "RemindOnNoReply", "ResponseDeadline" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalAttachments_InternalMailId",
                table: "InternalAttachments",
                column: "InternalMailId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalMails_InstitutionId_MailDate",
                table: "InternalMails",
                columns: new[] { "InstitutionId", "MailDate" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalMails_InstitutionId_MailNumber",
                table: "InternalMails",
                columns: new[] { "InstitutionId", "MailNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalMails_InstitutionId_Status",
                table: "InternalMails",
                columns: new[] { "InstitutionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalMails_RemindOnNoReply_ResponseDeadline",
                table: "InternalMails",
                columns: new[] { "RemindOnNoReply", "ResponseDeadline" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalReads_InternalMailId_UserId",
                table: "InternalReads",
                columns: new[] { "InternalMailId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InternalReads_UserId_ReadStatus",
                table: "InternalReads",
                columns: new[] { "UserId", "ReadStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalRecipients_InternalMailId_Addressing",
                table: "InternalRecipients",
                columns: new[] { "InternalMailId", "Addressing" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiwanAttachments");

            migrationBuilder.DropTable(
                name: "InternalAttachments");

            migrationBuilder.DropTable(
                name: "InternalReads");

            migrationBuilder.DropTable(
                name: "InternalRecipients");

            migrationBuilder.DropTable(
                name: "DiwanMails");

            migrationBuilder.DropTable(
                name: "InternalMails");

            migrationBuilder.CreateTable(
                name: "CorrespondenceTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    NameAr = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    ParentCorrespondenceId = table.Column<long>(type: "bigint", nullable: true),
                    ReplyToId = table.Column<long>(type: "bigint", nullable: true),
                    TypeId = table.Column<long>(type: "bigint", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: true),
                    AddresseeName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    AddresseeTitle = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Body = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    CorrespondenceDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CorrespondenceNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true),
                    Description = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FileNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    IsArchived = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Keywords = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    NoReplyNotifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    RegistrationDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RemindOnNoReply = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ResponseDeadline = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ResponsePeriodDays = table.Column<int>(type: "int", nullable: true),
                    SenderId = table.Column<long>(type: "bigint", nullable: true),
                    SenderKind = table.Column<int>(type: "int", nullable: false),
                    SenderName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Subject = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true)
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
                    Checksum = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    ContentType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    FileName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    OcrStatus = table.Column<int>(type: "int", nullable: false),
                    OcrText = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
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
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    LinkType = table.Column<int>(type: "int", nullable: false),
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
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FirstReadAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastReadAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ReadStatus = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
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
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    TargetId = table.Column<long>(type: "bigint", nullable: true),
                    TargetKind = table.Column<int>(type: "int", nullable: false),
                    TargetName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
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
    }
}
