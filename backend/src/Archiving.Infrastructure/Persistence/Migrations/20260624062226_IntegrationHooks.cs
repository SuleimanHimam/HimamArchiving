using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Archiving.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntegrationHooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntegrationHooks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    CallbackUrl = table.Column<string>(type: "varchar(1024)", maxLength: 1024, nullable: false),
                    HttpMethod = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    HeadersJson = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    SecretKey = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false),
                    RetryDelaySeconds = table.Column<int>(type: "int", nullable: false),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationHooks", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "WebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    IntegrationHookId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    PayloadJson = table.Column<string>(type: "longtext", maxLength: 256, nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "longtext", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveries", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationHooks_EventType_IsActive",
                table: "IntegrationHooks",
                columns: new[] { "EventType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_IntegrationHookId",
                table: "WebhookDeliveries",
                column: "IntegrationHookId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_Status_NextAttemptAt",
                table: "WebhookDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntegrationHooks");

            migrationBuilder.DropTable(
                name: "WebhookDeliveries");
        }
    }
}
