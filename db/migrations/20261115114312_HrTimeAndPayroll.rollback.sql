BEGIN TRANSACTION;
IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    ALTER TABLE [OrgUnits] DROP CONSTRAINT [FK_OrgUnits_sites_SiteId];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[attendance_exceptions];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[attendance_periods];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[attendance_punches];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[coverage_rules];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[disbursement_rules];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[employee_pay_elements];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[entitlement_movements];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[leave_balance_movements];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[pay_element_rates];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[payroll_inputs];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[payroll_result_lines];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[payroll_stage_log];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[public_holidays];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[salary_scale_rates];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[settings];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[shift_assignments];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[shift_rosters];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[tax_brackets];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[pay_elements];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[payroll_results];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[leave_requests];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[shift_types];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[payroll_periods];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP TABLE [hr].[leave_types];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DROP INDEX [IX_OrgUnits_SiteId] ON [OrgUnits];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrgUnits]') AND [c].[name] = N'SiteId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [OrgUnits] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [OrgUnits] DROP COLUMN [SiteId];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261115114312_HrTimeAndPayroll';
END;

COMMIT;
GO

