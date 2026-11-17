BEGIN TRANSACTION;
IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114110408_HrEmployeeFileAndActions'
)
BEGIN
    DROP TABLE [hr].[clearance_items];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114110408_HrEmployeeFileAndActions'
)
BEGIN
    DROP TABLE [hr].[employee_bank_accounts];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114110408_HrEmployeeFileAndActions'
)
BEGIN
    DROP TABLE [hr].[employee_family_members];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114110408_HrEmployeeFileAndActions'
)
BEGIN
    DROP TABLE [hr].[employee_profiles];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114110408_HrEmployeeFileAndActions'
)
BEGIN
    DROP TABLE [hr].[employee_qualifications];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114110408_HrEmployeeFileAndActions'
)
BEGIN
    DROP TABLE [hr].[clearances];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114110408_HrEmployeeFileAndActions'
)
BEGIN
    DROP TABLE [core].[lookups];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114110408_HrEmployeeFileAndActions'
)
BEGIN
    DROP TABLE [hr].[personnel_actions];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114110408_HrEmployeeFileAndActions'
)
BEGIN
    DROP TABLE [core].[approval_requests];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114110408_HrEmployeeFileAndActions'
)
BEGIN
    DROP TABLE [hr].[personnel_action_types];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114110408_HrEmployeeFileAndActions'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114110408_HrEmployeeFileAndActions';
END;

COMMIT;
GO

