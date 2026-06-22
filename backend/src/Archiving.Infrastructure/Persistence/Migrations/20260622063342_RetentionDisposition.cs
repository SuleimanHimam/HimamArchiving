using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RetentionDisposition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultAction",
                table: "RetentionPolicies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "RetentionPolicies",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresLegalApprovalForRenewal",
                table: "RetentionPolicies",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TriggerType",
                table: "RetentionPolicies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DispositionCertificates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DispositionRequestId = table.Column<long>(type: "bigint", nullable: false),
                    CertificateNumber = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    DocumentIds = table.Column<string>(type: "longtext", maxLength: 256, nullable: false),
                    DestructionMethod = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    VerifiedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    FinalApprovedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PdfStorageKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispositionCertificates", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DispositionRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RequestedAction = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "longtext", maxLength: 256, nullable: false),
                    RequestedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VerifiedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VerificationNotes = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    FinalApprovedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    FinalApprovedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    FinalApprovalNotes = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    RejectedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RejectionReason = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    NewExpiryDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Method = table.Column<int>(type: "int", nullable: false),
                    CustomMethod = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    CertificateId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispositionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DispositionRequests_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DocumentRetentions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    RetentionPolicyId = table.Column<long>(type: "bigint", nullable: true),
                    TriggerDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OriginalExpiryDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentRetentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentRetentions_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentRetentions_RetentionPolicies_RetentionPolicyId",
                        column: x => x.RetentionPolicyId,
                        principalTable: "RetentionPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DispositionCertificates_CertificateNumber",
                table: "DispositionCertificates",
                column: "CertificateNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DispositionCertificates_DispositionRequestId",
                table: "DispositionCertificates",
                column: "DispositionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DispositionRequests_DocumentId",
                table: "DispositionRequests",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DispositionRequests_RequestedAction",
                table: "DispositionRequests",
                column: "RequestedAction");

            migrationBuilder.CreateIndex(
                name: "IX_DispositionRequests_Status",
                table: "DispositionRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRetentions_DocumentId",
                table: "DocumentRetentions",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRetentions_ExpiryDate",
                table: "DocumentRetentions",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRetentions_RetentionPolicyId",
                table: "DocumentRetentions",
                column: "RetentionPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentRetentions_Status",
                table: "DocumentRetentions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DispositionCertificates");

            migrationBuilder.DropTable(
                name: "DispositionRequests");

            migrationBuilder.DropTable(
                name: "DocumentRetentions");

            migrationBuilder.DropColumn(
                name: "DefaultAction",
                table: "RetentionPolicies");

            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                table: "RetentionPolicies");

            migrationBuilder.DropColumn(
                name: "RequiresLegalApprovalForRenewal",
                table: "RetentionPolicies");

            migrationBuilder.DropColumn(
                name: "TriggerType",
                table: "RetentionPolicies");
        }
    }
}
