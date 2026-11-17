BEGIN TRANSACTION;
IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114221005_HrActionJsonLongText'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[personnel_actions]') AND [c].[name] = N'payload_json');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [hr].[personnel_actions] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [hr].[personnel_actions] ALTER COLUMN [payload_json] nvarchar(256) NOT NULL;
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114221005_HrActionJsonLongText'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[personnel_actions]') AND [c].[name] = N'before_json');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [hr].[personnel_actions] DROP CONSTRAINT ' + @var1 + ';');
    ALTER TABLE [hr].[personnel_actions] ALTER COLUMN [before_json] nvarchar(256) NULL;
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114221005_HrActionJsonLongText'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[hr].[personnel_actions]') AND [c].[name] = N'after_json');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [hr].[personnel_actions] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [hr].[personnel_actions] ALTER COLUMN [after_json] nvarchar(256) NULL;
END;

IF EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114221005_HrActionJsonLongText'
)
BEGIN
    DELETE FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261114221005_HrActionJsonLongText';
END;

COMMIT;
GO

