BEGIN TRANSACTION;
IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[count_committee_members];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[count_entries];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[count_variances];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[custodies];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[document_lines];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[issue_request_lines];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[monthly_quotas];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[settings];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[stock_movements];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[documents];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[count_cycles];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[issue_requests];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[opening_balance_imports];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[work_orders];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[assets];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[items];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[warehouses];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DROP TABLE [supply].[item_categories];
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261118144953_SupplyAndWarehouses';
END;

COMMIT;
GO

