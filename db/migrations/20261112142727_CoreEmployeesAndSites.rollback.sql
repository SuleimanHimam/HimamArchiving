BEGIN TRANSACTION;
IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112142727_CoreEmployeesAndSites'
)
BEGIN
    DROP TABLE [core].[employees];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112142727_CoreEmployeesAndSites'
)
BEGIN
    DROP TABLE [core].[site_locations];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112142727_CoreEmployeesAndSites'
)
BEGIN
    DROP TABLE [core].[sites];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112142727_CoreEmployeesAndSites'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261112142727_CoreEmployeesAndSites';
END;

COMMIT;
GO

