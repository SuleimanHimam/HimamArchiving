using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class HrEmployeeFileAndActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hr");

            migrationBuilder.CreateTable(
                name: "approval_requests",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    request_type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    source_module = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    requester_user_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    workflow_instance_id = table.Column<long>(type: "bigint", nullable: true),
                    decided_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    decision_note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_approval_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_approval_requests_Users_requester_user_id",
                        column: x => x.requester_user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_approval_requests_WorkflowInstances_workflow_instance_id",
                        column: x => x.workflow_instance_id,
                        principalTable: "WorkflowInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_bank_accounts",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    bank_name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    account_enc = table.Column<byte[]>(type: "varbinary(512)", maxLength: 512, nullable: false),
                    is_primary = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_bank_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_bank_accounts_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lookups",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lookups", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "personnel_action_types",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    effect = table.Column<int>(type: "int", nullable: false),
                    workflow_definition_id = table.Column<long>(type: "bigint", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personnel_action_types", x => x.id);
                    table.ForeignKey(
                        name: "FK_personnel_action_types_WorkflowDefinitions_workflow_definition_id",
                        column: x => x.workflow_definition_id,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_family_members",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    full_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    relationship_id = table.Column<long>(type: "bigint", nullable: false),
                    birth_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_family_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_family_members_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_family_members_lookups_relationship_id",
                        column: x => x.relationship_id,
                        principalSchema: "core",
                        principalTable: "lookups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_profiles",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    birth_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    marital_status_id = table.Column<long>(type: "bigint", nullable: true),
                    address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    appointment_type_id = table.Column<long>(type: "bigint", nullable: true),
                    grade_id = table.Column<long>(type: "bigint", nullable: true),
                    national_id_enc = table.Column<byte[]>(type: "varbinary(512)", maxLength: 512, nullable: true),
                    mobile_enc = table.Column<byte[]>(type: "varbinary(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_profiles", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_profiles_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_profiles_lookups_appointment_type_id",
                        column: x => x.appointment_type_id,
                        principalSchema: "core",
                        principalTable: "lookups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_profiles_lookups_grade_id",
                        column: x => x.grade_id,
                        principalSchema: "core",
                        principalTable: "lookups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_profiles_lookups_marital_status_id",
                        column: x => x.marital_status_id,
                        principalSchema: "core",
                        principalTable: "lookups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_qualifications",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    qualification_type_id = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    institution = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    year = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_qualifications", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_qualifications_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_qualifications_lookups_qualification_type_id",
                        column: x => x.qualification_type_id,
                        principalSchema: "core",
                        principalTable: "lookups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "personnel_actions",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    action_type_id = table.Column<long>(type: "bigint", nullable: false),
                    employee_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    effective_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    payload_json = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    before_json = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    after_json = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    approval_request_id = table.Column<long>(type: "bigint", nullable: true),
                    executed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    decision_note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_personnel_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_personnel_actions_approval_requests_approval_request_id",
                        column: x => x.approval_request_id,
                        principalSchema: "core",
                        principalTable: "approval_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_personnel_actions_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_personnel_actions_personnel_action_types_action_type_id",
                        column: x => x.action_type_id,
                        principalSchema: "hr",
                        principalTable: "personnel_action_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clearances",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    personnel_action_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    issued_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clearances", x => x.id);
                    table.ForeignKey(
                        name: "FK_clearances_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clearances_personnel_actions_personnel_action_id",
                        column: x => x.personnel_action_id,
                        principalSchema: "hr",
                        principalTable: "personnel_actions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "clearance_items",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    clearance_id = table.Column<long>(type: "bigint", nullable: false),
                    party_id = table.Column<long>(type: "bigint", nullable: false),
                    is_cleared = table.Column<bool>(type: "bit", nullable: false),
                    cleared_by = table.Column<long>(type: "bigint", nullable: true),
                    cleared_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    created_by = table.Column<long>(type: "bigint", nullable: true),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    updated_by = table.Column<long>(type: "bigint", nullable: true),
                    is_deleted = table.Column<bool>(type: "bit", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    deleted_by = table.Column<long>(type: "bigint", nullable: true),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clearance_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_clearance_items_clearances_clearance_id",
                        column: x => x.clearance_id,
                        principalSchema: "hr",
                        principalTable: "clearances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_clearance_items_lookups_party_id",
                        column: x => x.party_id,
                        principalSchema: "core",
                        principalTable: "lookups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_number",
                schema: "core",
                table: "approval_requests",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_requester_user_id",
                schema: "core",
                table: "approval_requests",
                column: "requester_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_source_module_source_id",
                schema: "core",
                table: "approval_requests",
                columns: new[] { "source_module", "source_id" });

            migrationBuilder.CreateIndex(
                name: "IX_approval_requests_workflow_instance_id",
                schema: "core",
                table: "approval_requests",
                column: "workflow_instance_id");

            migrationBuilder.CreateIndex(
                name: "IX_clearance_items_clearance_id_party_id",
                schema: "hr",
                table: "clearance_items",
                columns: new[] { "clearance_id", "party_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clearance_items_party_id",
                schema: "hr",
                table: "clearance_items",
                column: "party_id");

            migrationBuilder.CreateIndex(
                name: "IX_clearances_employee_id",
                schema: "hr",
                table: "clearances",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_clearances_number",
                schema: "hr",
                table: "clearances",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_clearances_personnel_action_id",
                schema: "hr",
                table: "clearances",
                column: "personnel_action_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_bank_accounts_employee_id",
                schema: "hr",
                table: "employee_bank_accounts",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_family_members_employee_id",
                schema: "hr",
                table: "employee_family_members",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_family_members_relationship_id",
                schema: "hr",
                table: "employee_family_members",
                column: "relationship_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_profiles_appointment_type_id",
                schema: "hr",
                table: "employee_profiles",
                column: "appointment_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_profiles_employee_id",
                schema: "hr",
                table: "employee_profiles",
                column: "employee_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_profiles_grade_id",
                schema: "hr",
                table: "employee_profiles",
                column: "grade_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_profiles_marital_status_id",
                schema: "hr",
                table: "employee_profiles",
                column: "marital_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_qualifications_employee_id",
                schema: "hr",
                table: "employee_qualifications",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_qualifications_qualification_type_id",
                schema: "hr",
                table: "employee_qualifications",
                column: "qualification_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_lookups_category_code",
                schema: "core",
                table: "lookups",
                columns: new[] { "category", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personnel_action_types_code",
                schema: "hr",
                table: "personnel_action_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_personnel_action_types_workflow_definition_id",
                schema: "hr",
                table: "personnel_action_types",
                column: "workflow_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_actions_action_type_id",
                schema: "hr",
                table: "personnel_actions",
                column: "action_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_actions_approval_request_id",
                schema: "hr",
                table: "personnel_actions",
                column: "approval_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_actions_employee_id",
                schema: "hr",
                table: "personnel_actions",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_actions_number",
                schema: "hr",
                table: "personnel_actions",
                column: "number",
                unique: true,
                filter: "[number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_personnel_actions_status",
                schema: "hr",
                table: "personnel_actions",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clearance_items",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "employee_bank_accounts",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "employee_family_members",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "employee_profiles",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "employee_qualifications",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "clearances",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "lookups",
                schema: "core");

            migrationBuilder.DropTable(
                name: "personnel_actions",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "approval_requests",
                schema: "core");

            migrationBuilder.DropTable(
                name: "personnel_action_types",
                schema: "hr");
        }
    }
}
