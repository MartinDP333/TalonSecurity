BEGIN TRANSACTION;
DROP INDEX [IX_TenantMemberships_TenantId_UserId] ON [TenantMemberships];

ALTER TABLE [TenantMemberships] ADD [UserAccountId] uniqueidentifier NULL;


            UPDATE tm
            SET UserAccountId = ua.Id
            FROM TenantMemberships tm
            INNER JOIN UserAccounts ua ON ua.UserName = tm.UserId;

            IF EXISTS (SELECT 1 FROM TenantMemberships WHERE UserAccountId IS NULL)
                THROW 50001, 'Every tenant membership must match a UserAccount before migration.', 1;
        

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TenantMemberships]') AND [c].[name] = N'UserAccountId');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [TenantMemberships] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [TenantMemberships] ALTER COLUMN [UserAccountId] uniqueidentifier NOT NULL;

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TenantMemberships]') AND [c].[name] = N'UserId');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [TenantMemberships] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [TenantMemberships] DROP COLUMN [UserId];

CREATE UNIQUE INDEX [IX_TenantMemberships_TenantId_UserAccountId] ON [TenantMemberships] ([TenantId], [UserAccountId]);

CREATE INDEX [IX_TenantMemberships_UserAccountId] ON [TenantMemberships] ([UserAccountId]);

ALTER TABLE [Id].[TenantMemberships] ADD CONSTRAINT [FK_TenantMemberships_UserAccounts_UserAccountId] FOREIGN KEY ([UserAccountId]) REFERENCES [UserAccounts] ON DELETE CASCADE;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261021184617_LinkMembershipsToUserAccounts', N'10.0.12');

COMMIT;
GO

