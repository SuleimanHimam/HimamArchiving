using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Archiving.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class SupplyAndWarehouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "supply");

            migrationBuilder.CreateTable(
                name: "item_categories",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    parent_id = table.Column<long>(type: "bigint", nullable: true),
                    code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_item_categories", x => x.id);
                    table.ForeignKey(
                        name: "FK_item_categories_item_categories_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "supply",
                        principalTable: "item_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "opening_balance_imports",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    file_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    stage = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    row_count = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    errors_json = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    document_id = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_opening_balance_imports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                schema: "supply",
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
                name: "warehouses",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
                    site_id = table.Column<long>(type: "bigint", nullable: true),
                    keeper_employee_id = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_warehouses", x => x.id);
                    table.ForeignKey(
                        name: "FK_warehouses_employees_keeper_employee_id",
                        column: x => x.keeper_employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_warehouses_sites_site_id",
                        column: x => x.site_id,
                        principalSchema: "core",
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "items",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    category_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
                    unit = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    reorder_level = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    min_level = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    useful_life_months = table.Column<int>(type: "int", nullable: true),
                    warranty_months = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_items_item_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "supply",
                        principalTable: "item_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "count_cycles",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    scope = table.Column<int>(type: "int", nullable: false),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: true),
                    org_unit_id = table.Column<long>(type: "bigint", nullable: true),
                    site_id = table.Column<long>(type: "bigint", nullable: true),
                    planned_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    freeze_movements = table.Column<bool>(type: "bit", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    adjustment_document_id = table.Column<long>(type: "bigint", nullable: true),
                    closed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_count_cycles", x => x.id);
                    table.ForeignKey(
                        name: "FK_count_cycles_OrgUnits_org_unit_id",
                        column: x => x.org_unit_id,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_count_cycles_sites_site_id",
                        column: x => x.site_id,
                        principalSchema: "core",
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_count_cycles_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "supply",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "issue_requests",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    requester_employee_id = table.Column<long>(type: "bigint", nullable: false),
                    org_unit_id = table.Column<long>(type: "bigint", nullable: false),
                    for_department = table.Column<bool>(type: "bit", nullable: false),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: false),
                    justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    monthly_quota_run_id = table.Column<long>(type: "bigint", nullable: true),
                    approval_request_id = table.Column<long>(type: "bigint", nullable: true),
                    decided_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    suspended_at = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    table.PrimaryKey("PK_issue_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_issue_requests_OrgUnits_org_unit_id",
                        column: x => x.org_unit_id,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issue_requests_approval_requests_approval_request_id",
                        column: x => x.approval_request_id,
                        principalSchema: "core",
                        principalTable: "approval_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issue_requests_employees_requester_employee_id",
                        column: x => x.requester_employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issue_requests_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "supply",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "assets",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    asset_number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    barcode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    serial_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    condition = table.Column<int>(type: "int", nullable: false),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: true),
                    purchase_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    warranty_end_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    last_counted_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    status_before_maintenance = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_assets", x => x.id);
                    table.ForeignKey(
                        name: "FK_assets_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "supply",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_assets_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "supply",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "monthly_quotas",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    org_unit_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
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
                    table.PrimaryKey("PK_monthly_quotas", x => x.id);
                    table.ForeignKey(
                        name: "FK_monthly_quotas_OrgUnits_org_unit_id",
                        column: x => x.org_unit_id,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_monthly_quotas_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "supply",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_monthly_quotas_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "supply",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "count_committee_members",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    count_cycle_id = table.Column<long>(type: "bigint", nullable: false),
                    employee_id = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_count_committee_members", x => x.id);
                    table.ForeignKey(
                        name: "FK_count_committee_members_count_cycles_count_cycle_id",
                        column: x => x.count_cycle_id,
                        principalSchema: "supply",
                        principalTable: "count_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_count_committee_members_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "issue_request_lines",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    issue_request_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    issued_quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
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
                    table.PrimaryKey("PK_issue_request_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_issue_request_lines_issue_requests_issue_request_id",
                        column: x => x.issue_request_id,
                        principalSchema: "supply",
                        principalTable: "issue_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_issue_request_lines_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "supply",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "count_entries",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    count_cycle_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: true),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: true),
                    quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    barcode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    asset_id = table.Column<long>(type: "bigint", nullable: true),
                    condition = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_count_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_count_entries_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "supply",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_count_entries_count_cycles_count_cycle_id",
                        column: x => x.count_cycle_id,
                        principalSchema: "supply",
                        principalTable: "count_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_count_entries_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "supply",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_count_entries_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "supply",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "count_variances",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    count_cycle_id = table.Column<long>(type: "bigint", nullable: false),
                    kind = table.Column<int>(type: "int", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: true),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: true),
                    asset_id = table.Column<long>(type: "bigint", nullable: true),
                    barcode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    expected = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    counted = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    difference = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    counted_condition = table.Column<int>(type: "int", nullable: true),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    justification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_count_variances", x => x.id);
                    table.ForeignKey(
                        name: "FK_count_variances_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "supply",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_count_variances_count_cycles_count_cycle_id",
                        column: x => x.count_cycle_id,
                        principalSchema: "supply",
                        principalTable: "count_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_count_variances_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "supply",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_count_variances_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "supply",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_orders",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    asset_id = table.Column<long>(type: "bigint", nullable: true),
                    location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    site_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    reporter_employee_id = table.Column<long>(type: "bigint", nullable: true),
                    assignee_employee_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<int>(type: "int", nullable: false),
                    assessment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    labor_hours = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    labor_cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    parts_cost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    closed_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    outcome = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_work_orders", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_orders_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "supply",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_orders_employees_assignee_employee_id",
                        column: x => x.assignee_employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_orders_employees_reporter_employee_id",
                        column: x => x.reporter_employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_orders_sites_site_id",
                        column: x => x.site_id,
                        principalSchema: "core",
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    number = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    type = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<int>(type: "int", nullable: false),
                    doc_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: true),
                    target_warehouse_id = table.Column<long>(type: "bigint", nullable: true),
                    recipient_employee_id = table.Column<long>(type: "bigint", nullable: true),
                    recipient_org_unit_id = table.Column<long>(type: "bigint", nullable: true),
                    supplier = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    invoice_number = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    received_by_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    issue_request_id = table.Column<long>(type: "bigint", nullable: true),
                    count_cycle_id = table.Column<long>(type: "bigint", nullable: true),
                    work_order_id = table.Column<long>(type: "bigint", nullable: true),
                    opening_import_id = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_documents_OrgUnits_recipient_org_unit_id",
                        column: x => x.recipient_org_unit_id,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_approval_requests_approval_request_id",
                        column: x => x.approval_request_id,
                        principalSchema: "core",
                        principalTable: "approval_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_count_cycles_count_cycle_id",
                        column: x => x.count_cycle_id,
                        principalSchema: "supply",
                        principalTable: "count_cycles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_employees_recipient_employee_id",
                        column: x => x.recipient_employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_issue_requests_issue_request_id",
                        column: x => x.issue_request_id,
                        principalSchema: "supply",
                        principalTable: "issue_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_opening_balance_imports_opening_import_id",
                        column: x => x.opening_import_id,
                        principalSchema: "supply",
                        principalTable: "opening_balance_imports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_warehouses_target_warehouse_id",
                        column: x => x.target_warehouse_id,
                        principalSchema: "supply",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "supply",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_documents_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalSchema: "supply",
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "custodies",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    asset_id = table.Column<long>(type: "bigint", nullable: false),
                    holder_type = table.Column<int>(type: "int", nullable: false),
                    employee_id = table.Column<long>(type: "bigint", nullable: true),
                    org_unit_id = table.Column<long>(type: "bigint", nullable: true),
                    holder_org_unit_id = table.Column<long>(type: "bigint", nullable: true),
                    from_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    to_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    opened_by_document_id = table.Column<long>(type: "bigint", nullable: false),
                    closed_by_document_id = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_custodies", x => x.id);
                    table.ForeignKey(
                        name: "FK_custodies_OrgUnits_org_unit_id",
                        column: x => x.org_unit_id,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_custodies_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "supply",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_custodies_documents_closed_by_document_id",
                        column: x => x.closed_by_document_id,
                        principalSchema: "supply",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_custodies_documents_opened_by_document_id",
                        column: x => x.opened_by_document_id,
                        principalSchema: "supply",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_custodies_employees_employee_id",
                        column: x => x.employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_lines",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    asset_id = table.Column<long>(type: "bigint", nullable: true),
                    serial_number = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    barcode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    condition = table.Column<int>(type: "int", nullable: true),
                    unit_value = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    purchase_date = table.Column<DateTime>(type: "datetime2", nullable: true),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: true),
                    holder_employee_id = table.Column<long>(type: "bigint", nullable: true),
                    holder_org_unit_id = table.Column<long>(type: "bigint", nullable: true),
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
                    table.PrimaryKey("PK_document_lines", x => x.id);
                    table.ForeignKey(
                        name: "FK_document_lines_OrgUnits_holder_org_unit_id",
                        column: x => x.holder_org_unit_id,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_lines_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "supply",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_lines_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "supply",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_lines_employees_holder_employee_id",
                        column: x => x.holder_employee_id,
                        principalSchema: "core",
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_lines_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "supply",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_document_lines_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "supply",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                schema: "supply",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    item_id = table.Column<long>(type: "bigint", nullable: false),
                    warehouse_id = table.Column<long>(type: "bigint", nullable: false),
                    quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    document_id = table.Column<long>(type: "bigint", nullable: false),
                    asset_id = table.Column<long>(type: "bigint", nullable: true),
                    org_unit_id = table.Column<long>(type: "bigint", nullable: true),
                    work_order_id = table.Column<long>(type: "bigint", nullable: true),
                    movement_date = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_stock_movements", x => x.id);
                    table.ForeignKey(
                        name: "FK_stock_movements_OrgUnits_org_unit_id",
                        column: x => x.org_unit_id,
                        principalTable: "OrgUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_assets_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "supply",
                        principalTable: "assets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "supply",
                        principalTable: "documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_items_item_id",
                        column: x => x.item_id,
                        principalSchema: "supply",
                        principalTable: "items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_warehouses_warehouse_id",
                        column: x => x.warehouse_id,
                        principalSchema: "supply",
                        principalTable: "warehouses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_stock_movements_work_orders_work_order_id",
                        column: x => x.work_order_id,
                        principalSchema: "supply",
                        principalTable: "work_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assets_asset_number",
                schema: "supply",
                table: "assets",
                column: "asset_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assets_barcode",
                schema: "supply",
                table: "assets",
                column: "barcode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assets_item_id",
                schema: "supply",
                table: "assets",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_assets_serial_number",
                schema: "supply",
                table: "assets",
                column: "serial_number",
                filter: "[serial_number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_assets_status",
                schema: "supply",
                table: "assets",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_assets_warehouse_id",
                schema: "supply",
                table: "assets",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_count_committee_members_count_cycle_id_employee_id",
                schema: "supply",
                table: "count_committee_members",
                columns: new[] { "count_cycle_id", "employee_id" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_count_committee_members_employee_id",
                schema: "supply",
                table: "count_committee_members",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_count_cycles_number",
                schema: "supply",
                table: "count_cycles",
                column: "number",
                unique: true,
                filter: "[number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_count_cycles_org_unit_id",
                schema: "supply",
                table: "count_cycles",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_count_cycles_site_id",
                schema: "supply",
                table: "count_cycles",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_count_cycles_status",
                schema: "supply",
                table: "count_cycles",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_count_cycles_warehouse_id",
                schema: "supply",
                table: "count_cycles",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_count_entries_asset_id",
                schema: "supply",
                table: "count_entries",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_count_entries_count_cycle_id",
                schema: "supply",
                table: "count_entries",
                column: "count_cycle_id");

            migrationBuilder.CreateIndex(
                name: "IX_count_entries_item_id",
                schema: "supply",
                table: "count_entries",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_count_entries_warehouse_id",
                schema: "supply",
                table: "count_entries",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_count_variances_asset_id",
                schema: "supply",
                table: "count_variances",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_count_variances_count_cycle_id",
                schema: "supply",
                table: "count_variances",
                column: "count_cycle_id");

            migrationBuilder.CreateIndex(
                name: "IX_count_variances_item_id",
                schema: "supply",
                table: "count_variances",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_count_variances_warehouse_id",
                schema: "supply",
                table: "count_variances",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_custodies_asset_id_to_date",
                schema: "supply",
                table: "custodies",
                columns: new[] { "asset_id", "to_date" });

            migrationBuilder.CreateIndex(
                name: "IX_custodies_closed_by_document_id",
                schema: "supply",
                table: "custodies",
                column: "closed_by_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_custodies_employee_id",
                schema: "supply",
                table: "custodies",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_custodies_opened_by_document_id",
                schema: "supply",
                table: "custodies",
                column: "opened_by_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_custodies_org_unit_id",
                schema: "supply",
                table: "custodies",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_lines_asset_id",
                schema: "supply",
                table: "document_lines",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_lines_document_id",
                schema: "supply",
                table: "document_lines",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_lines_holder_employee_id",
                schema: "supply",
                table: "document_lines",
                column: "holder_employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_lines_holder_org_unit_id",
                schema: "supply",
                table: "document_lines",
                column: "holder_org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_lines_item_id",
                schema: "supply",
                table: "document_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_document_lines_warehouse_id",
                schema: "supply",
                table: "document_lines",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_approval_request_id",
                schema: "supply",
                table: "documents",
                column: "approval_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_count_cycle_id",
                schema: "supply",
                table: "documents",
                column: "count_cycle_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_issue_request_id",
                schema: "supply",
                table: "documents",
                column: "issue_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_number",
                schema: "supply",
                table: "documents",
                column: "number",
                unique: true,
                filter: "[number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_documents_opening_import_id",
                schema: "supply",
                table: "documents",
                column: "opening_import_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_recipient_employee_id",
                schema: "supply",
                table: "documents",
                column: "recipient_employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_recipient_org_unit_id",
                schema: "supply",
                table: "documents",
                column: "recipient_org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_target_warehouse_id",
                schema: "supply",
                table: "documents",
                column: "target_warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_type_status",
                schema: "supply",
                table: "documents",
                columns: new[] { "type", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_warehouse_id",
                schema: "supply",
                table: "documents",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_work_order_id",
                schema: "supply",
                table: "documents",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_issue_request_lines_issue_request_id",
                schema: "supply",
                table: "issue_request_lines",
                column: "issue_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_issue_request_lines_item_id",
                schema: "supply",
                table: "issue_request_lines",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_issue_requests_approval_request_id",
                schema: "supply",
                table: "issue_requests",
                column: "approval_request_id");

            migrationBuilder.CreateIndex(
                name: "IX_issue_requests_number",
                schema: "supply",
                table: "issue_requests",
                column: "number",
                unique: true,
                filter: "[number] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_issue_requests_org_unit_id_monthly_quota_run_id",
                schema: "supply",
                table: "issue_requests",
                columns: new[] { "org_unit_id", "monthly_quota_run_id" });

            migrationBuilder.CreateIndex(
                name: "IX_issue_requests_requester_employee_id",
                schema: "supply",
                table: "issue_requests",
                column: "requester_employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_issue_requests_status",
                schema: "supply",
                table: "issue_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_issue_requests_warehouse_id",
                schema: "supply",
                table: "issue_requests",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_item_categories_code",
                schema: "supply",
                table: "item_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_categories_parent_id",
                schema: "supply",
                table: "item_categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_category_id",
                schema: "supply",
                table: "items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_items_code",
                schema: "supply",
                table: "items",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monthly_quotas_item_id",
                schema: "supply",
                table: "monthly_quotas",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_quotas_org_unit_id_item_id",
                schema: "supply",
                table: "monthly_quotas",
                columns: new[] { "org_unit_id", "item_id" },
                unique: true,
                filter: "[is_deleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_quotas_warehouse_id",
                schema: "supply",
                table: "monthly_quotas",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_settings_key",
                schema: "supply",
                table: "settings",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_asset_id",
                schema: "supply",
                table: "stock_movements",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_document_id",
                schema: "supply",
                table: "stock_movements",
                column: "document_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_item_id_warehouse_id",
                schema: "supply",
                table: "stock_movements",
                columns: new[] { "item_id", "warehouse_id" });

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_movement_date",
                schema: "supply",
                table: "stock_movements",
                column: "movement_date");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_org_unit_id",
                schema: "supply",
                table: "stock_movements",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_warehouse_id",
                schema: "supply",
                table: "stock_movements",
                column: "warehouse_id");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_work_order_id",
                schema: "supply",
                table: "stock_movements",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_code",
                schema: "supply",
                table: "warehouses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_keeper_employee_id",
                schema: "supply",
                table: "warehouses",
                column: "keeper_employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_site_id",
                schema: "supply",
                table: "warehouses",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_asset_id",
                schema: "supply",
                table: "work_orders",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_assignee_employee_id",
                schema: "supply",
                table: "work_orders",
                column: "assignee_employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_number",
                schema: "supply",
                table: "work_orders",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_reporter_employee_id",
                schema: "supply",
                table: "work_orders",
                column: "reporter_employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_site_id",
                schema: "supply",
                table: "work_orders",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_status",
                schema: "supply",
                table: "work_orders",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "count_committee_members",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "count_entries",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "count_variances",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "custodies",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "document_lines",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "issue_request_lines",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "monthly_quotas",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "settings",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "stock_movements",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "count_cycles",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "issue_requests",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "opening_balance_imports",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "work_orders",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "assets",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "items",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "warehouses",
                schema: "supply");

            migrationBuilder.DropTable(
                name: "item_categories",
                schema: "supply");
        }
    }
}
