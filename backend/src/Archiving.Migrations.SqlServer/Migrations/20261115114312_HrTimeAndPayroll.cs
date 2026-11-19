using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class HrTimeAndPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "SiteId",
                table: "OrgUnits",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "attendance_periods",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    year = table.Column<int>(type: "int", nullable: false),
                    month = table.Column<int>(type: "int", nullable: false),
                    closed_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    closed_by = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_attendance_periods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attendance_punches",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    punched_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    source = table.Column<int>(type: "int", nullable: false),
                    device_code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_attendance_punches", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_punches_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leave_types",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
                    unit = table.Column<int>(type: "int", nullable: false),
                    deducts_balance = table.Column<bool>(type: "bit", nullable: false),
                    count_working_days_only = table.Column<bool>(type: "bit", nullable: false),
                    requires_substitute = table.Column<bool>(type: "bit", nullable: false),
                    annual_entitlement = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    max_carry_over = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    applies_to_military = table.Column<bool>(type: "bit", nullable: false),
                    applies_to_civil = table.Column<bool>(type: "bit", nullable: false),
                    applies_to_contract = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_leave_types", x => x.id);
                    table.ForeignKey(
                        name: "FK_leave_types_WorkflowDefinitions_workflow_definition_id",
                        column: x => x.workflow_definition_id,
                        principalTable: "WorkflowDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pay_elements",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
                    method = table.Column<int>(type: "int", nullable: false),
                    source = table.Column<int>(type: "int", nullable: false),
                    is_taxable = table.Column<bool>(type: "bit", nullable: false),
                    is_pre_tax = table.Column<bool>(type: "bit", nullable: false),
                    applies_to_military = table.Column<bool>(type: "bit", nullable: false),
                    applies_to_civil = table.Column<bool>(type: "bit", nullable: false),
                    applies_to_contract = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_pay_elements", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payroll_periods",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    year = table.Column<int>(type: "int", nullable: false),
                    month = table.Column<int>(type: "int", nullable: false),
                    stage = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_payroll_periods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "public_holidays",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
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
                    table.PrimaryKey("PK_public_holidays", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "salary_scale_rates",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    grade_id = table.Column<long>(type: "bigint", nullable: false),
                    category = table.Column<int>(type: "int", nullable: true),
                    effective_from = table.Column<DateTime>(type: "datetime2", nullable: false),
                    basic_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_salary_scale_rates", x => x.id);
                    table.ForeignKey(
                        name: "FK_salary_scale_rates_lookups_grade_id",
                        column: x => x.grade_id,
                        principalSchema: "core",
                        principalTable: "lookups",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
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
                    table.PrimaryKey("PK_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "shift_rosters",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    site_id = table.Column<long>(type: "bigint", nullable: false),
                    year = table.Column<int>(type: "int", nullable: false),
                    month = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    published_at = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_shift_rosters", x => x.id);
                    table.ForeignKey(
                        name: "FK_shift_rosters_sites_site_id",
                        column: x => x.site_id,
                        principalSchema: "core",
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shift_types",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time", nullable: false),
                    grace_minutes = table.Column<int>(type: "int", nullable: false),
                    is_night = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_shift_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_brackets",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    effective_from = table.Column<DateTime>(type: "datetime2", nullable: false),
                    from_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    to_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    rate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_tax_brackets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leave_balance_movements",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    leave_type_id = table.Column<long>(type: "bigint", nullable: false),
                    year = table.Column<int>(type: "int", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
                    source_type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    source_id = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_leave_balance_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_leave_balance_movements_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_balance_movements_leave_types_leave_type_id",
                        column: x => x.leave_type_id,
                        principalSchema: "hr",
                        principalTable: "leave_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leave_requests",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    leave_type_id = table.Column<long>(type: "bigint", nullable: false),
                    from_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    to_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    from_time = table.Column<TimeOnly>(type: "time", nullable: true),
                    to_time = table.Column<TimeOnly>(type: "time", nullable: true),
                    quantity = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    substitute_employee_id = table.Column<long>(type: "bigint", nullable: true),
                    reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    approval_request_id = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_leave_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_leave_requests_approval_requests_approval_request_id",
                        column: x => x.approval_request_id,
                        principalSchema: "core",
                        principalTable: "approval_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_requests_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_requests_employees_substitute_employee_id",
                        column: x => x.substitute_employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_leave_requests_leave_types_leave_type_id",
                        column: x => x.leave_type_id,
                        principalSchema: "hr",
                        principalTable: "leave_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_pay_elements",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    pay_element_id = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    from_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    to_date = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_employee_pay_elements", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_pay_elements_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_pay_elements_pay_elements_pay_element_id",
                        column: x => x.pay_element_id,
                        principalSchema: "hr",
                        principalTable: "pay_elements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pay_element_rates",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    pay_element_id = table.Column<long>(type: "bigint", nullable: false),
                    category = table.Column<int>(type: "int", nullable: true),
                    effective_from = table.Column<DateTime>(type: "datetime2", nullable: false),
                    value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_pay_element_rates", x => x.id);
                    table.ForeignKey(
                        name: "FK_pay_element_rates_pay_elements_pay_element_id",
                        column: x => x.pay_element_id,
                        principalSchema: "hr",
                        principalTable: "pay_elements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "disbursement_rules",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payroll_period_id = table.Column<long>(type: "bigint", nullable: false),
                    category = table.Column<int>(type: "int", nullable: false),
                    percent_of_salary = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    minimum_amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    percent_of_accumulated = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
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
                    table.PrimaryKey("PK_disbursement_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_disbursement_rules_payroll_periods_payroll_period_id",
                        column: x => x.payroll_period_id,
                        principalSchema: "hr",
                        principalTable: "payroll_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "entitlement_movements",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    payroll_period_id = table.Column<long>(type: "bigint", nullable: true),
                    kind = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_entitlement_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_entitlement_movements_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_entitlement_movements_payroll_periods_payroll_period_id",
                        column: x => x.payroll_period_id,
                        principalSchema: "hr",
                        principalTable: "payroll_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payroll_inputs",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payroll_period_id = table.Column<long>(type: "bigint", nullable: false),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    pay_element_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_payroll_inputs", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_inputs_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payroll_inputs_pay_elements_pay_element_id",
                        column: x => x.pay_element_id,
                        principalSchema: "hr",
                        principalTable: "pay_elements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payroll_inputs_payroll_periods_payroll_period_id",
                        column: x => x.payroll_period_id,
                        principalSchema: "hr",
                        principalTable: "payroll_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payroll_results",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payroll_period_id = table.Column<long>(type: "bigint", nullable: false),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    category = table.Column<int>(type: "int", nullable: false),
                    gross = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    deductions = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    net = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    paid = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_payroll_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_results_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payroll_results_payroll_periods_payroll_period_id",
                        column: x => x.payroll_period_id,
                        principalSchema: "hr",
                        principalTable: "payroll_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payroll_stage_log",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payroll_period_id = table.Column<long>(type: "bigint", nullable: false),
                    stage = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<long>(type: "bigint", nullable: true),
                    at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    is_return = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_payroll_stage_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_stage_log_Users_user_id",
                        column: x => x.user_id,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payroll_stage_log_payroll_periods_payroll_period_id",
                        column: x => x.payroll_period_id,
                        principalSchema: "hr",
                        principalTable: "payroll_periods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attendance_exceptions",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
                    minutes = table.Column<int>(type: "int", nullable: false),
                    shift_type_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    resolution = table.Column<int>(type: "int", nullable: true),
                    resolution_note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    resolved_by = table.Column<long>(type: "bigint", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_attendance_exceptions", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_exceptions_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_attendance_exceptions_shift_types_shift_type_id",
                        column: x => x.shift_type_id,
                        principalSchema: "hr",
                        principalTable: "shift_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coverage_rules",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    site_id = table.Column<long>(type: "bigint", nullable: false),
                    shift_type_id = table.Column<long>(type: "bigint", nullable: false),
                    min_count = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_coverage_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_coverage_rules_shift_types_shift_type_id",
                        column: x => x.shift_type_id,
                        principalSchema: "hr",
                        principalTable: "shift_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_coverage_rules_sites_site_id",
                        column: x => x.site_id,
                        principalSchema: "core",
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shift_assignments",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
                    date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
                    shift_type_id = table.Column<long>(type: "bigint", nullable: true),
                    site_id = table.Column<long>(type: "bigint", nullable: true),
                    leave_request_id = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_shift_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_shift_assignments_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shift_assignments_leave_requests_leave_request_id",
                        column: x => x.leave_request_id,
                        principalSchema: "hr",
                        principalTable: "leave_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shift_assignments_shift_types_shift_type_id",
                        column: x => x.shift_type_id,
                        principalSchema: "hr",
                        principalTable: "shift_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_shift_assignments_sites_site_id",
                        column: x => x.site_id,
                        principalSchema: "core",
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payroll_result_lines",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    payroll_result_id = table.Column<long>(type: "bigint", nullable: false),
                    pay_element_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    rate = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
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
                    table.PrimaryKey("PK_payroll_result_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_payroll_result_lines_pay_elements_pay_element_id",
                        column: x => x.pay_element_id,
                        principalSchema: "hr",
                        principalTable: "pay_elements",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_payroll_result_lines_payroll_results_payroll_result_id",
                        column: x => x.payroll_result_id,
                        principalSchema: "hr",
                        principalTable: "payroll_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrgUnits_SiteId",
                table: "OrgUnits",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_exceptions_employee_id_date_kind",
                schema: "hr",
                table: "attendance_exceptions",
                columns: new[] { "employee_id", "date", "kind" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_exceptions_shift_type_id",
                schema: "hr",
                table: "attendance_exceptions",
                column: "shift_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_exceptions_status_date",
                schema: "hr",
                table: "attendance_exceptions",
                columns: new[] { "status", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_attendance_periods_year_month",
                schema: "hr",
                table: "attendance_periods",
                columns: new[] { "year", "month" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_attendance_punches_employee_id_punched_at",
                schema: "hr",
                table: "attendance_punches",
                columns: new[] { "employee_id", "punched_at" });

            migrationBuilder.CreateIndex(
                name: "IX_coverage_rules_shift_type_id",
                schema: "hr",
                table: "coverage_rules",
                column: "shift_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_coverage_rules_site_id_shift_type_id",
                schema: "hr",
                table: "coverage_rules",
                columns: new[] { "site_id", "shift_type_id" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_disbursement_rules_payroll_period_id_category",
                schema: "hr",
                table: "disbursement_rules",
                columns: new[] { "payroll_period_id", "category" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_employee_pay_elements_employee_id_pay_element_id",
                schema: "hr",
                table: "employee_pay_elements",
                columns: new[] { "employee_id", "pay_element_id" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_pay_elements_pay_element_id",
                schema: "hr",
                table: "employee_pay_elements",
                column: "pay_element_id");

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_movements_employee_id",
                schema: "hr",
                table: "entitlement_movements",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_entitlement_movements_payroll_period_id",
                schema: "hr",
                table: "entitlement_movements",
                column: "payroll_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_balance_movements_employee_id_leave_type_id_year",
                schema: "hr",
                table: "leave_balance_movements",
                columns: new[] { "employee_id", "leave_type_id", "year" });

            migrationBuilder.CreateIndex(
                name: "IX_leave_balance_movements_leave_type_id",
                schema: "hr",
                table: "leave_balance_movements",
                column: "leave_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_approval_request_id",
                schema: "hr",
                table: "leave_requests",
                column: "approval_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_employee_id_from_date",
                schema: "hr",
                table: "leave_requests",
                columns: new[] { "employee_id", "from_date" });

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_leave_type_id",
                schema: "hr",
                table: "leave_requests",
                column: "leave_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_number",
                schema: "hr",
                table: "leave_requests",
                column: "number",
                unique: true,
                filter: "[number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_status",
                schema: "hr",
                table: "leave_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_leave_requests_substitute_employee_id",
                schema: "hr",
                table: "leave_requests",
                column: "substitute_employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_leave_types_code",
                schema: "hr",
                table: "leave_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_leave_types_workflow_definition_id",
                schema: "hr",
                table: "leave_types",
                column: "workflow_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_pay_element_rates_pay_element_id_effective_from",
                schema: "hr",
                table: "pay_element_rates",
                columns: new[] { "pay_element_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "IX_pay_elements_code",
                schema: "hr",
                table: "pay_elements",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payroll_inputs_employee_id",
                schema: "hr",
                table: "payroll_inputs",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_inputs_pay_element_id",
                schema: "hr",
                table: "payroll_inputs",
                column: "pay_element_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_inputs_payroll_period_id_employee_id",
                schema: "hr",
                table: "payroll_inputs",
                columns: new[] { "payroll_period_id", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payroll_periods_year_month",
                schema: "hr",
                table: "payroll_periods",
                columns: new[] { "year", "month" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_result_lines_pay_element_id",
                schema: "hr",
                table: "payroll_result_lines",
                column: "pay_element_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_result_lines_payroll_result_id",
                schema: "hr",
                table: "payroll_result_lines",
                column: "payroll_result_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_results_employee_id",
                schema: "hr",
                table: "payroll_results",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_results_payroll_period_id_employee_id",
                schema: "hr",
                table: "payroll_results",
                columns: new[] { "payroll_period_id", "employee_id" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_stage_log_payroll_period_id",
                schema: "hr",
                table: "payroll_stage_log",
                column: "payroll_period_id");

            migrationBuilder.CreateIndex(
                name: "IX_payroll_stage_log_user_id",
                schema: "hr",
                table: "payroll_stage_log",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_public_holidays_date",
                schema: "hr",
                table: "public_holidays",
                column: "date",
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_salary_scale_rates_grade_id_effective_from",
                schema: "hr",
                table: "salary_scale_rates",
                columns: new[] { "grade_id", "effective_from" });

            migrationBuilder.CreateIndex(
                name: "IX_settings_key",
                schema: "hr",
                table: "settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shift_assignments_employee_id_date",
                schema: "hr",
                table: "shift_assignments",
                columns: new[] { "employee_id", "date" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_shift_assignments_leave_request_id",
                schema: "hr",
                table: "shift_assignments",
                column: "leave_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_assignments_shift_type_id",
                schema: "hr",
                table: "shift_assignments",
                column: "shift_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_shift_assignments_site_id_date",
                schema: "hr",
                table: "shift_assignments",
                columns: new[] { "site_id", "date" });

            migrationBuilder.CreateIndex(
                name: "IX_shift_rosters_site_id_year_month",
                schema: "hr",
                table: "shift_rosters",
                columns: new[] { "site_id", "year", "month" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_shift_types_code",
                schema: "hr",
                table: "shift_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tax_brackets_effective_from",
                schema: "hr",
                table: "tax_brackets",
                column: "effective_from");

            migrationBuilder.AddForeignKey(
                name: "FK_OrgUnits_sites_SiteId",
                table: "OrgUnits",
                column: "SiteId",
                principalSchema: "core",
                principalTable: "sites",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrgUnits_sites_SiteId",
                table: "OrgUnits");

            migrationBuilder.DropTable(
                name: "attendance_exceptions",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "attendance_periods",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "attendance_punches",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "coverage_rules",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "disbursement_rules",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "employee_pay_elements",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "entitlement_movements",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "leave_balance_movements",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "pay_element_rates",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "payroll_inputs",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "payroll_result_lines",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "payroll_stage_log",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "public_holidays",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "salary_scale_rates",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "settings",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "shift_assignments",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "shift_rosters",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "tax_brackets",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "pay_elements",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "payroll_results",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "leave_requests",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "shift_types",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "payroll_periods",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "leave_types",
                schema: "hr");

            migrationBuilder.DropIndex(
                name: "IX_OrgUnits_SiteId",
                table: "OrgUnits");

            migrationBuilder.DropColumn(
                name: "SiteId",
                table: "OrgUnits");
        }
    }
}
