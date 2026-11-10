using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqlServer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EntityId = table.Column<long>(type: "bigint", nullable: true),
                    EntityTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OldValues = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    NewValues = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    MachineName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsHidden = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AutoBackupSettings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    TargetPath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IntervalHours = table.Column<int>(type: "int", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunStatus = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastRunError = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutoBackupSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClassificationTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassificationTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FieldId = table.Column<long>(type: "bigint", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldValues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyMetricSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: true),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    MetricType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Breakdown = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyMetricSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DesignatedCommunities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RenderingExpectations = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesignatedCommunities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DestructionCertificates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DestructionRequestId = table.Column<long>(type: "bigint", nullable: false),
                    CertificateNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PdfStorageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestructionCertificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DestructionMethodOptions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Label = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestructionMethodOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DestructionRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    RetentionBasisId = table.Column<long>(type: "bigint", nullable: true),
                    RequestedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ExecutedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    ScheduledForUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CertificateId = table.Column<long>(type: "bigint", nullable: true),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestructionRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DispositionCertificates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DispositionRequestId = table.Column<long>(type: "bigint", nullable: false),
                    CertificateNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DocumentIds = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    DestructionMethod = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    VerifiedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    FinalApprovedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PdfStorageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispositionCertificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentCategories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentCategories_DocumentCategories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "DocumentCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EscalationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: false),
                    WorkflowStageId = table.Column<long>(type: "bigint", nullable: false),
                    EscalationRuleId = table.Column<long>(type: "bigint", nullable: false),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    EscalatedToPositionId = table.Column<long>(type: "bigint", nullable: false),
                    ActionTaken = table.Column<int>(type: "int", nullable: false),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EscalationRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionId = table.Column<long>(type: "bigint", nullable: true),
                    WorkflowStageId = table.Column<long>(type: "bigint", nullable: true),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    OverdueMinutes = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    EscalateToPositionId = table.Column<long>(type: "bigint", nullable: true),
                    NotifyOriginator = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EventLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EventType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EventData = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    TriggeredByUserId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExternalOrganizations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalOrganizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Folders",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Folders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Institutions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LogoStorageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LogoBase64 = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    ColorPrimary = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ColorAccent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ColorSeal = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ColorBg = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ColorRailBg = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ColorRailText = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ColorRailAccent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ColorNavBg = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ColorNavText = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ColorLoginBg = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ColorLoginAccent = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    HiddenNavKeys = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationHooks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CallbackUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    HeadersJson = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    SecretKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false),
                    RetryDelaySeconds = table.Column<int>(type: "int", nullable: false),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationHooks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LegalHolds",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: true),
                    FolderId = table.Column<long>(type: "bigint", nullable: true),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    QueryExpression = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    PlacedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    PlacedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReleasedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ReleasedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalHolds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MailCategories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MailCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberingTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: true),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    DocumentTypeId = table.Column<long>(type: "bigint", nullable: true),
                    Pattern = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ResetFrequency = table.Column<int>(type: "int", nullable: false),
                    CurrentSequence = table.Column<int>(type: "int", nullable: false),
                    CurrentYear = table.Column<int>(type: "int", nullable: true),
                    CurrentMonth = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberingTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Resource = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalLocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RfidTag = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalLocations_PhysicalLocations_ParentId",
                        column: x => x.ParentId,
                        principalTable: "PhysicalLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PreservationPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TargetPdfAConformance = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AutoNormalizeOnIngest = table.Column<bool>(type: "bit", nullable: false),
                    FixityAlgorithm = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FixityCadenceDays = table.Column<int>(type: "int", nullable: false),
                    AllowedPreservationFormats = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreservationPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecordAgents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecordType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    RecordId = table.Column<long>(type: "bigint", nullable: false),
                    AgentKind = table.Column<int>(type: "int", nullable: false),
                    AgentId = table.Column<long>(type: "bigint", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordAgents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecordRelationships",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SourceId = table.Column<long>(type: "bigint", nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TargetId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordRelationships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserTablePrefs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TableKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ConfigJson = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTablePrefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IntegrationHookId = table.Column<long>(type: "bigint", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowDefinitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TriggerModule = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DocumentTypeId = table.Column<long>(type: "bigint", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BuildingId = table.Column<long>(type: "bigint", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RoomNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Floor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorrespondenceGroupMembers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<long>(type: "bigint", nullable: false),
                    MemberType = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorrespondenceGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorrespondenceGroupMembers_CorrespondenceGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "CorrespondenceGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DestructionItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DestructionRequestId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    CustomMethod = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ChecksumBefore = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestructionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DestructionItems_DestructionRequests_DestructionRequestId",
                        column: x => x.DestructionRequestId,
                        principalTable: "DestructionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalContacts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalContacts_ExternalOrganizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "ExternalOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrgUnits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ManagerPositionId = table.Column<long>(type: "bigint", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgUnits_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrgUnits_OrgUnits_ParentId",
                        column: x => x.ParentId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiwanMails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    RegisterNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    ExternalOrgId = table.Column<long>(type: "bigint", nullable: true),
                    ExternalOrgName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AddresseeName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AddresseeTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    Keywords = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MailDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponsePeriodDays = table.Column<int>(type: "int", nullable: true),
                    RemindOnNoReply = table.Column<bool>(type: "bit", nullable: false),
                    NoReplyNotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true),
                    ReferenceToId = table.Column<long>(type: "bigint", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: true),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FileNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchivedDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    ReplyToId = table.Column<long>(type: "bigint", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiwanMails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiwanMails_ExternalOrganizations_ExternalOrgId",
                        column: x => x.ExternalOrgId,
                        principalTable: "ExternalOrganizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiwanMails_MailCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "MailCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InternalMails",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    MailNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SenderUserId = table.Column<long>(type: "bigint", nullable: false),
                    SenderName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SenderOrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    Keywords = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MailDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RegistrationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseDeadline = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponsePeriodDays = table.Column<int>(type: "int", nullable: true),
                    RemindOnNoReply = table.Column<bool>(type: "bit", nullable: false),
                    NoReplyNotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: true),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FileNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchivedDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    ReplyToId = table.Column<long>(type: "bigint", nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InternalMails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InternalMails_MailCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "MailCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoleClassifications",
                columns: table => new
                {
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ClassificationTypeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClassifications", x => new { x.RoleId, x.ClassificationTypeId });
                    table.ForeignKey(
                        name: "FK_RoleClassifications_ClassificationTypes_ClassificationTypeId",
                        column: x => x.ClassificationTypeId,
                        principalTable: "ClassificationTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoleClassifications_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    PermissionId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowStages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    AssigneeType = table.Column<int>(type: "int", nullable: false),
                    AssigneePositionId = table.Column<long>(type: "bigint", nullable: true),
                    AssigneeOrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    AssigneeRoleId = table.Column<long>(type: "bigint", nullable: true),
                    AssigneeUserId = table.Column<long>(type: "bigint", nullable: true),
                    ResponseHours = table.Column<int>(type: "int", nullable: false),
                    EscalateAfterHours = table.Column<int>(type: "int", nullable: true),
                    EscalateToPositionId = table.Column<long>(type: "bigint", nullable: true),
                    AllowedActions = table.Column<int>(type: "int", nullable: false),
                    TransitionCondition = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    RequiresCorrespondence = table.Column<bool>(type: "bit", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ConditionExpression = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ParallelGroupId = table.Column<int>(type: "int", nullable: true),
                    RequiresAllParallelComplete = table.Column<bool>(type: "bit", nullable: false),
                    AssignmentType = table.Column<int>(type: "int", nullable: false),
                    AssignmentExpression = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowStages_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Cabinets",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomId = table.Column<long>(type: "bigint", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CabinetCode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ShelfCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cabinets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cabinets_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RoomConnections",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomId = table.Column<long>(type: "bigint", nullable: false),
                    ConnectedRoomId = table.Column<long>(type: "bigint", nullable: false),
                    ConnectionType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomConnections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomConnections_Rooms_ConnectedRoomId",
                        column: x => x.ConnectedRoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RoomConnections_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContactChannels",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactChannels_ExternalContacts_ContactId",
                        column: x => x.ContactId,
                        principalTable: "ExternalContacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTypes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Code = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    Number = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    DefaultConfidentiality = table.Column<int>(type: "int", nullable: false),
                    RetentionValue = table.Column<int>(type: "int", nullable: false),
                    RetentionUnit = table.Column<int>(type: "int", nullable: false),
                    RetentionMonths = table.Column<int>(type: "int", nullable: false),
                    DefaultWorkflowDefinitionId = table.Column<long>(type: "bigint", nullable: true),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    RequiresPhysicalArchiving = table.Column<bool>(type: "bit", nullable: false),
                    AllowedUploadSources = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTypes_DocumentCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "DocumentCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentTypes_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SecondName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ThirdName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FamilyName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    JobTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    PasswordHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Clearance = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AvatarUrl = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    SignatureUrl = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    StampUrl = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    DefaultPrintTemplateId = table.Column<long>(type: "bigint", nullable: true),
                    DefaultMailTemplateId = table.Column<long>(type: "bigint", nullable: true),
                    MfaEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MfaMethod = table.Column<int>(type: "int", nullable: false),
                    MfaSecret = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DirectoryLogin = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiwanAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiwanMailId = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    OcrText = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    OcrStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                });

            migrationBuilder.CreateTable(
                name: "DiwanBureaus",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DiwanMailId = table.Column<long>(type: "bigint", nullable: false),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiwanBureaus", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiwanBureaus_DiwanMails_DiwanMailId",
                        column: x => x.DiwanMailId,
                        principalTable: "DiwanMails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiwanBureaus_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InternalAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InternalMailId = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    OcrText = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    OcrStatus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                });

            migrationBuilder.CreateTable(
                name: "InternalReads",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InternalMailId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ReadStatus = table.Column<int>(type: "int", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FirstReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                });

            migrationBuilder.CreateTable(
                name: "InternalRecipients",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InternalMailId = table.Column<long>(type: "bigint", nullable: false),
                    Addressing = table.Column<int>(type: "int", nullable: false),
                    TargetKind = table.Column<int>(type: "int", nullable: false),
                    TargetId = table.Column<long>(type: "bigint", nullable: true),
                    TargetName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                });

            migrationBuilder.CreateTable(
                name: "WorkflowInstances",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowDefinitionId = table.Column<long>(type: "bigint", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    CurrentStageId = table.Column<long>(type: "bigint", nullable: true),
                    CurrentAssigneePositionId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InitiatedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowInstances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_WorkflowDefinitions_WorkflowDefinitionId",
                        column: x => x.WorkflowDefinitionId,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowInstances_WorkflowStages_CurrentStageId",
                        column: x => x.CurrentStageId,
                        principalTable: "WorkflowStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Shelves",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CabinetId = table.Column<long>(type: "bigint", nullable: false),
                    ShelfNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shelves", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shelves_Cabinets_CabinetId",
                        column: x => x.CabinetId,
                        principalTable: "Cabinets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DocumentTypeId = table.Column<long>(type: "bigint", nullable: true),
                    FieldKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FieldType = table.Column<int>(type: "int", nullable: false),
                    Options = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    Searchable = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFieldDefinitions_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReportTemplates",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitutionId = table.Column<long>(type: "bigint", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    Target = table.Column<int>(type: "int", nullable: false),
                    DocumentTypeId = table.Column<long>(type: "bigint", nullable: true),
                    MailCategoryId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LogoBase64 = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    LayoutJson = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SlotsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LegacyLayoutJson = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ApprovalWorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportTemplates_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportTemplates_Institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportTemplates_MailCategories_MailCategoryId",
                        column: x => x.MailCategoryId,
                        principalTable: "MailCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReportTemplates_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetentionPolicies",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentTypeId = table.Column<long>(type: "bigint", nullable: false),
                    RetentionMonths = table.Column<int>(type: "int", nullable: false),
                    DisposalAction = table.Column<int>(type: "int", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TriggerType = table.Column<int>(type: "int", nullable: false),
                    DefaultAction = table.Column<int>(type: "int", nullable: false),
                    RequiresLegalApprovalForRenewal = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetentionPolicies_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipientUserId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Channels = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EntityId = table.Column<long>(type: "bigint", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsEscalation = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_RecipientUserId",
                        column: x => x.RecipientUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Positions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OrgUnitId = table.Column<long>(type: "bigint", nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    CurrentOccupantUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Positions_OrgUnits_OrgUnitId",
                        column: x => x.OrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Positions_Users_CurrentOccupantUserId",
                        column: x => x.CurrentOccupantUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Token = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByIp = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WorkflowTasks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkflowInstanceId = table.Column<long>(type: "bigint", nullable: false),
                    StageId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedToPositionId = table.Column<long>(type: "bigint", nullable: true),
                    AssignedToUserId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActionTaken = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    IsEscalated = table.Column<bool>(type: "bit", nullable: false),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkflowTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkflowTasks_WorkflowInstances_WorkflowInstanceId",
                        column: x => x.WorkflowInstanceId,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkflowTasks_WorkflowStages_StageId",
                        column: x => x.StageId,
                        principalTable: "WorkflowStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Boxes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShelfId = table.Column<long>(type: "bigint", nullable: true),
                    RoomId = table.Column<long>(type: "bigint", nullable: true),
                    BoxCode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Barcode = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Capacity = table.Column<int>(type: "int", nullable: true),
                    CurrentCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boxes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Boxes_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Boxes_Shelves_ShelfId",
                        column: x => x.ShelfId,
                        principalTable: "Shelves",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NotificationDeliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationId = table.Column<long>(type: "bigint", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationDeliveries_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    DocumentTypeId = table.Column<long>(type: "bigint", nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: true),
                    OwningOrgUnitId = table.Column<long>(type: "bigint", nullable: false),
                    OwnerPositionId = table.Column<long>(type: "bigint", nullable: true),
                    Confidentiality = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Keywords = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    RetentionMonths = table.Column<int>(type: "int", nullable: false),
                    DocumentDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ParentDocumentId = table.Column<long>(type: "bigint", nullable: true),
                    IsLatestVersion = table.Column<bool>(type: "bit", nullable: false),
                    FolderId = table.Column<long>(type: "bigint", nullable: true),
                    BoxId = table.Column<long>(type: "bigint", nullable: true),
                    IsTombstone = table.Column<bool>(type: "bit", nullable: false),
                    DestroyedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DestructionCertificateId = table.Column<long>(type: "bigint", nullable: true),
                    ArchiveWorkflowInstanceId = table.Column<long>(type: "bigint", nullable: true),
                    ArchiveReturnReason = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ArchiveMode = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_DocumentCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "DocumentCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_DocumentTypes_DocumentTypeId",
                        column: x => x.DocumentTypeId,
                        principalTable: "DocumentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Documents_ParentDocumentId",
                        column: x => x.ParentDocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_OrgUnits_OwningOrgUnitId",
                        column: x => x.OwningOrgUnitId,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Documents_Positions_OwnerPositionId",
                        column: x => x.OwnerPositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PositionAssignments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PositionId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionAssignments_Positions_PositionId",
                        column: x => x.PositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PositionAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PositionDelegations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromPositionId = table.Column<long>(type: "bigint", nullable: false),
                    ToPositionId = table.Column<long>(type: "bigint", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    WorkflowDefinitionId = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionDelegations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionDelegations_Positions_FromPositionId",
                        column: x => x.FromPositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PositionDelegations_Positions_ToPositionId",
                        column: x => x.ToPositionId,
                        principalTable: "Positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DisposalRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ApprovedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Justification = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DisposalRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DisposalRequests_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DispositionRequests",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedAction = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    RequestedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    VerifiedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerificationNotes = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    FinalApprovedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    FinalApprovedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalApprovalNotes = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    RejectedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    RejectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    NewExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Method = table.Column<int>(type: "int", nullable: false),
                    CustomMethod = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    CertificateId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                });

            migrationBuilder.CreateTable(
                name: "DocumentAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FileExtension = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ChecksumAlgorithm = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastFixityCheckAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsScanned = table.Column<bool>(type: "bit", nullable: false),
                    IsEncrypted = table.Column<bool>(type: "bit", nullable: false),
                    EncryptionIv = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContentDestroyed = table.Column<bool>(type: "bit", nullable: false),
                    ContentDestroyedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExtractedText = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    ExtractionStatus = table.Column<int>(type: "int", nullable: false),
                    ExtractionSource = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    TextExtractedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    SourceAttachmentId = table.Column<long>(type: "bigint", nullable: true),
                    PdfAConformance = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PreservationValidated = table.Column<bool>(type: "bit", nullable: false),
                    PreservationNote = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentAttachments_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentFavorites",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentFavorites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentFavorites_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentNotes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentNotes_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentRetentions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    RetentionPolicyId = table.Column<long>(type: "bigint", nullable: true),
                    TriggerDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OriginalExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                });

            migrationBuilder.CreateTable(
                name: "DocumentShares",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    SharedWithUserId = table.Column<long>(type: "bigint", nullable: false),
                    SharedByUserId = table.Column<long>(type: "bigint", nullable: false),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentShares_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InformationPackages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    FileCount = table.Column<int>(type: "int", nullable: false),
                    TotalBytes = table.Column<long>(type: "bigint", nullable: false),
                    Manifest = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InformationPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InformationPackages_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalArchiveItems",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: true),
                    PhysicalLocationId = table.Column<long>(type: "bigint", nullable: false),
                    BoxNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FileNumber = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ArchivedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalArchiveItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhysicalArchiveItems_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalArchiveItems_PhysicalLocations_PhysicalLocationId",
                        column: x => x.PhysicalLocationId,
                        principalTable: "PhysicalLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RetentionAlerts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NotifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetentionAlerts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetentionAlerts_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FixityChecks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentAttachmentId = table.Column<long>(type: "bigint", nullable: false),
                    Algorithm = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ExpectedHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ActualHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Result = table.Column<int>(type: "int", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CheckedByUserId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixityChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FixityChecks_DocumentAttachments_DocumentAttachmentId",
                        column: x => x.DocumentAttachmentId,
                        principalTable: "DocumentAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RepresentationInfos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DocumentAttachmentId = table.Column<long>(type: "bigint", nullable: false),
                    FormatName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    MimeType = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    PronomPuid = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    RenderingNote = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<long>(type: "bigint", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepresentationInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepresentationInfos_DocumentAttachments_DocumentAttachmentId",
                        column: x => x.DocumentAttachmentId,
                        principalTable: "DocumentAttachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Boxes_BoxCode",
                table: "Boxes",
                column: "BoxCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Boxes_RoomId",
                table: "Boxes",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Boxes_ShelfId",
                table: "Boxes",
                column: "ShelfId");

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_Code",
                table: "Buildings",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Cabinets_RoomId",
                table: "Cabinets",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ContactChannels_ContactId",
                table: "ContactChannels",
                column: "ContactId");

            migrationBuilder.CreateIndex(
                name: "IX_CorrespondenceGroupMembers_GroupId",
                table: "CorrespondenceGroupMembers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_DocumentTypeId",
                table: "CustomFieldDefinitions",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_EntityType",
                table: "CustomFieldDefinitions",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_EntityType_DocumentTypeId",
                table: "CustomFieldDefinitions",
                columns: new[] { "EntityType", "DocumentTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_EntityType_FieldKey",
                table: "CustomFieldDefinitions",
                columns: new[] { "EntityType", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_EntityType_EntityId",
                table: "CustomFieldValues",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_FieldId_EntityId",
                table: "CustomFieldValues",
                columns: new[] { "FieldId", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyMetricSnapshots_MetricType_Date",
                table: "DailyMetricSnapshots",
                columns: new[] { "MetricType", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_DestructionCertificates_DestructionRequestId",
                table: "DestructionCertificates",
                column: "DestructionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DestructionItems_DestructionRequestId",
                table: "DestructionItems",
                column: "DestructionRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_DestructionItems_DocumentId",
                table: "DestructionItems",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DestructionRequests_Status",
                table: "DestructionRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DisposalRequests_DocumentId",
                table: "DisposalRequests",
                column: "DocumentId");

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
                name: "IX_DiwanAttachments_DiwanMailId",
                table: "DiwanAttachments",
                column: "DiwanMailId");

            migrationBuilder.CreateIndex(
                name: "IX_DiwanBureaus_DiwanMailId_OrgUnitId",
                table: "DiwanBureaus",
                columns: new[] { "DiwanMailId", "OrgUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiwanBureaus_OrgUnitId",
                table: "DiwanBureaus",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_DiwanMails_CategoryId",
                table: "DiwanMails",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DiwanMails_ExternalOrgId",
                table: "DiwanMails",
                column: "ExternalOrgId");

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
                name: "IX_DocumentAttachments_DocumentId",
                table: "DocumentAttachments",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentAttachments_ExtractionStatus",
                table: "DocumentAttachments",
                column: "ExtractionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentCategories_ParentId",
                table: "DocumentCategories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFavorites_DocumentId",
                table: "DocumentFavorites",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentFavorites_UserId_DocumentId",
                table: "DocumentFavorites",
                columns: new[] { "UserId", "DocumentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentNotes_DocumentId",
                table: "DocumentNotes",
                column: "DocumentId");

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

            migrationBuilder.CreateIndex(
                name: "IX_Documents_BoxId",
                table: "Documents",
                column: "BoxId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CategoryId",
                table: "Documents",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentNumber",
                table: "Documents",
                column: "DocumentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_DocumentTypeId",
                table: "Documents",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OwnerPositionId",
                table: "Documents",
                column: "OwnerPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_OwningOrgUnitId",
                table: "Documents",
                column: "OwningOrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ParentDocumentId",
                table: "Documents",
                column: "ParentDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentShares_DocumentId_SharedWithUserId",
                table: "DocumentShares",
                columns: new[] { "DocumentId", "SharedWithUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_CategoryId",
                table: "DocumentTypes",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTypes_OrgUnitId",
                table: "DocumentTypes",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationLogs_WorkflowInstanceId_WorkflowStageId_EscalationLevel",
                table: "EscalationLogs",
                columns: new[] { "WorkflowInstanceId", "WorkflowStageId", "EscalationLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_WorkflowDefinitionId_WorkflowStageId_IsActive",
                table: "EscalationRules",
                columns: new[] { "WorkflowDefinitionId", "WorkflowStageId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EventLogs_EventType",
                table: "EventLogs",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_EventLogs_OccurredAt",
                table: "EventLogs",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalContacts_OrganizationId",
                table: "ExternalContacts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrganizations_Code",
                table: "ExternalOrganizations",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrganizations_Name",
                table: "ExternalOrganizations",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_FixityChecks_CheckedAt",
                table: "FixityChecks",
                column: "CheckedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FixityChecks_DocumentAttachmentId_CheckedAt",
                table: "FixityChecks",
                columns: new[] { "DocumentAttachmentId", "CheckedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Folders_UserId",
                table: "Folders",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_InformationPackages_DocumentId_Type",
                table: "InformationPackages",
                columns: new[] { "DocumentId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Institutions_Code",
                table: "Institutions",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationHooks_EventType_IsActive",
                table: "IntegrationHooks",
                columns: new[] { "EventType", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_InternalAttachments_InternalMailId",
                table: "InternalAttachments",
                column: "InternalMailId");

            migrationBuilder.CreateIndex(
                name: "IX_InternalMails_CategoryId",
                table: "InternalMails",
                column: "CategoryId");

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

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_ReleasedAtUtc",
                table: "LegalHolds",
                column: "ReleasedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LegalHolds_Scope_DocumentId",
                table: "LegalHolds",
                columns: new[] { "Scope", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_MailCategories_InstitutionId_IsActive",
                table: "MailCategories",
                columns: new[] { "InstitutionId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationDeliveries_NotificationId",
                table: "NotificationDeliveries",
                column: "NotificationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientUserId_IsRead",
                table: "Notifications",
                columns: new[] { "RecipientUserId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_NumberingTemplates_EntityType_InstitutionId_OrgUnitId_DocumentTypeId_IsActive",
                table: "NumberingTemplates",
                columns: new[] { "EntityType", "InstitutionId", "OrgUnitId", "DocumentTypeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_InstitutionId",
                table: "OrgUnits",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_ParentId",
                table: "OrgUnits",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_Code",
                table: "Permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalArchiveItems_DocumentId",
                table: "PhysicalArchiveItems",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalArchiveItems_PhysicalLocationId",
                table: "PhysicalArchiveItems",
                column: "PhysicalLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalLocations_ParentId",
                table: "PhysicalLocations",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionAssignments_PositionId",
                table: "PositionAssignments",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionAssignments_UserId",
                table: "PositionAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionDelegations_FromPositionId_IsActive",
                table: "PositionDelegations",
                columns: new[] { "FromPositionId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_PositionDelegations_ToPositionId_IsActive",
                table: "PositionDelegations",
                columns: new[] { "ToPositionId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Positions_CurrentOccupantUserId",
                table: "Positions",
                column: "CurrentOccupantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Positions_OrgUnitId",
                table: "Positions",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RecordAgents_RecordType_RecordId",
                table: "RecordAgents",
                columns: new[] { "RecordType", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordAgents_RecordType_RecordId_AgentKind_AgentId_Role",
                table: "RecordAgents",
                columns: new[] { "RecordType", "RecordId", "AgentKind", "AgentId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecordRelationships_SourceType_SourceId",
                table: "RecordRelationships",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_RecordRelationships_SourceType_SourceId_TargetType_TargetId_Type",
                table: "RecordRelationships",
                columns: new[] { "SourceType", "SourceId", "TargetType", "TargetId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplates_DocumentTypeId",
                table: "ReportTemplates",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplates_InstitutionId_Scope_Target",
                table: "ReportTemplates",
                columns: new[] { "InstitutionId", "Scope", "Target" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplates_InstitutionId_Target_Status",
                table: "ReportTemplates",
                columns: new[] { "InstitutionId", "Target", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplates_MailCategoryId",
                table: "ReportTemplates",
                column: "MailCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportTemplates_OrgUnitId",
                table: "ReportTemplates",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_RepresentationInfos_DocumentAttachmentId",
                table: "RepresentationInfos",
                column: "DocumentAttachmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RetentionAlerts_DocumentId",
                table: "RetentionAlerts",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_RetentionPolicies_DocumentTypeId",
                table: "RetentionPolicies",
                column: "DocumentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClassifications_ClassificationTypeId",
                table: "RoleClassifications",
                column: "ClassificationTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomConnections_ConnectedRoomId",
                table: "RoomConnections",
                column: "ConnectedRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomConnections_RoomId_ConnectedRoomId",
                table: "RoomConnections",
                columns: new[] { "RoomId", "ConnectedRoomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_BuildingId",
                table: "Rooms",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Shelves_CabinetId",
                table: "Shelves",
                column: "CabinetId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrgUnitId",
                table: "Users",
                column: "OrgUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTablePrefs_UserId_TableKey",
                table: "UserTablePrefs",
                columns: new[] { "UserId", "TableKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_IntegrationHookId",
                table: "WebhookDeliveries",
                column: "IntegrationHookId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_Status_NextAttemptAt",
                table: "WebhookDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_CurrentStageId",
                table: "WorkflowInstances",
                column: "CurrentStageId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowInstances_WorkflowDefinitionId",
                table: "WorkflowInstances",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStages_WorkflowDefinitionId",
                table: "WorkflowStages",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTasks_AssignedToPositionId_Status",
                table: "WorkflowTasks",
                columns: new[] { "AssignedToPositionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTasks_StageId",
                table: "WorkflowTasks",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTasks_WorkflowInstanceId",
                table: "WorkflowTasks",
                column: "WorkflowInstanceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "AutoBackupSettings");

            migrationBuilder.DropTable(
                name: "Boxes");

            migrationBuilder.DropTable(
                name: "ContactChannels");

            migrationBuilder.DropTable(
                name: "CorrespondenceGroupMembers");

            migrationBuilder.DropTable(
                name: "CustomFieldDefinitions");

            migrationBuilder.DropTable(
                name: "CustomFieldValues");

            migrationBuilder.DropTable(
                name: "DailyMetricSnapshots");

            migrationBuilder.DropTable(
                name: "DesignatedCommunities");

            migrationBuilder.DropTable(
                name: "DestructionCertificates");

            migrationBuilder.DropTable(
                name: "DestructionItems");

            migrationBuilder.DropTable(
                name: "DestructionMethodOptions");

            migrationBuilder.DropTable(
                name: "DisposalRequests");

            migrationBuilder.DropTable(
                name: "DispositionCertificates");

            migrationBuilder.DropTable(
                name: "DispositionRequests");

            migrationBuilder.DropTable(
                name: "DiwanAttachments");

            migrationBuilder.DropTable(
                name: "DiwanBureaus");

            migrationBuilder.DropTable(
                name: "DocumentFavorites");

            migrationBuilder.DropTable(
                name: "DocumentNotes");

            migrationBuilder.DropTable(
                name: "DocumentRetentions");

            migrationBuilder.DropTable(
                name: "DocumentShares");

            migrationBuilder.DropTable(
                name: "EscalationLogs");

            migrationBuilder.DropTable(
                name: "EscalationRules");

            migrationBuilder.DropTable(
                name: "EventLogs");

            migrationBuilder.DropTable(
                name: "FixityChecks");

            migrationBuilder.DropTable(
                name: "Folders");

            migrationBuilder.DropTable(
                name: "InformationPackages");

            migrationBuilder.DropTable(
                name: "IntegrationHooks");

            migrationBuilder.DropTable(
                name: "InternalAttachments");

            migrationBuilder.DropTable(
                name: "InternalReads");

            migrationBuilder.DropTable(
                name: "InternalRecipients");

            migrationBuilder.DropTable(
                name: "LegalHolds");

            migrationBuilder.DropTable(
                name: "NotificationDeliveries");

            migrationBuilder.DropTable(
                name: "NumberingTemplates");

            migrationBuilder.DropTable(
                name: "PhysicalArchiveItems");

            migrationBuilder.DropTable(
                name: "PositionAssignments");

            migrationBuilder.DropTable(
                name: "PositionDelegations");

            migrationBuilder.DropTable(
                name: "PreservationPolicies");

            migrationBuilder.DropTable(
                name: "RecordAgents");

            migrationBuilder.DropTable(
                name: "RecordRelationships");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "ReportTemplates");

            migrationBuilder.DropTable(
                name: "RepresentationInfos");

            migrationBuilder.DropTable(
                name: "RetentionAlerts");

            migrationBuilder.DropTable(
                name: "RoleClassifications");

            migrationBuilder.DropTable(
                name: "RolePermissions");

            migrationBuilder.DropTable(
                name: "RoomConnections");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "UserTablePrefs");

            migrationBuilder.DropTable(
                name: "WebhookDeliveries");

            migrationBuilder.DropTable(
                name: "WorkflowTasks");

            migrationBuilder.DropTable(
                name: "Shelves");

            migrationBuilder.DropTable(
                name: "ExternalContacts");

            migrationBuilder.DropTable(
                name: "CorrespondenceGroups");

            migrationBuilder.DropTable(
                name: "DestructionRequests");

            migrationBuilder.DropTable(
                name: "DiwanMails");

            migrationBuilder.DropTable(
                name: "RetentionPolicies");

            migrationBuilder.DropTable(
                name: "InternalMails");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PhysicalLocations");

            migrationBuilder.DropTable(
                name: "DocumentAttachments");

            migrationBuilder.DropTable(
                name: "ClassificationTypes");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "WorkflowInstances");

            migrationBuilder.DropTable(
                name: "Cabinets");

            migrationBuilder.DropTable(
                name: "ExternalOrganizations");

            migrationBuilder.DropTable(
                name: "MailCategories");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "WorkflowStages");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "DocumentTypes");

            migrationBuilder.DropTable(
                name: "Positions");

            migrationBuilder.DropTable(
                name: "WorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "Buildings");

            migrationBuilder.DropTable(
                name: "DocumentCategories");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "OrgUnits");

            migrationBuilder.DropTable(
                name: "Institutions");
        }
    }
}
