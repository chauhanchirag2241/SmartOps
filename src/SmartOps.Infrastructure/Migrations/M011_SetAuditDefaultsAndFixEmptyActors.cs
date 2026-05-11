//using FluentMigrator;
//using SmartOps.Shared.Configuration;

//namespace SmartOps.Infrastructure.Migrations;

//[Migration(011)]
//public sealed class M011_SetAuditDefaultsAndFixEmptyActors : Migration
//{
//    private const string EmptyUserId = "00000000-0000-0000-0000-000000000000";
//    private const string OldSeedUserId = "00000000-0000-0000-0000-000000000001";

//    public override void Up()
//    {
//        FixAuditColumns(DatabaseConfig.TableUsers);
//        FixAuditColumns(DatabaseConfig.TableRoles);
//        FixAuditColumns(DatabaseConfig.TablePermissions);
//        FixAuditColumns(DatabaseConfig.TableUserRoles);
//        FixAuditColumns(DatabaseConfig.TableRolePermissions);
//        FixAuditColumns(DatabaseConfig.TableSchools);
//        FixAuditColumns(DatabaseConfig.TableUserSchoolMappings);
//        FixAuditColumns(DatabaseConfig.TableRefreshTokens);
//    }

//    public override void Down()
//    {
//        ClearAuditDefaults(DatabaseConfig.TableUsers);
//        ClearAuditDefaults(DatabaseConfig.TableRoles);
//        ClearAuditDefaults(DatabaseConfig.TablePermissions);
//        ClearAuditDefaults(DatabaseConfig.TableUserRoles);
//        ClearAuditDefaults(DatabaseConfig.TableRolePermissions);
//        ClearAuditDefaults(DatabaseConfig.TableSchools);
//        ClearAuditDefaults(DatabaseConfig.TableUserSchoolMappings);
//        ClearAuditDefaults(DatabaseConfig.TableRefreshTokens);
//    }

//    private void FixAuditColumns(string tableName)
//    {
//        Execute.Sql($"""
//DO $$
//BEGIN
//    IF to_regclass('{DatabaseConfig.Schema_Global}.{tableName}') IS NOT NULL THEN
//        UPDATE {DatabaseConfig.Schema_Global}.{tableName}
//        SET createdby = '{DatabaseConfig.SystemUserId}'::uuid
//        WHERE createdby IN ('{EmptyUserId}'::uuid, '{OldSeedUserId}'::uuid);

//        UPDATE {DatabaseConfig.Schema_Global}.{tableName}
//        SET updatedby = createdby
//        WHERE updatedby IN ('{EmptyUserId}'::uuid, '{OldSeedUserId}'::uuid);

//        ALTER TABLE {DatabaseConfig.Schema_Global}.{tableName}
//            ALTER COLUMN createdby SET DEFAULT '{DatabaseConfig.SystemUserId}'::uuid;

//        ALTER TABLE {DatabaseConfig.Schema_Global}.{tableName}
//            ALTER COLUMN updatedby SET DEFAULT '{DatabaseConfig.SystemUserId}'::uuid;
//    END IF;
//END $$;
//""");
//    }

//    private void ClearAuditDefaults(string tableName)
//    {
//        Execute.Sql($"""
//DO $$
//BEGIN
//    IF to_regclass('{DatabaseConfig.Schema_Global}.{tableName}') IS NOT NULL THEN
//        ALTER TABLE {DatabaseConfig.Schema_Global}.{tableName}
//            ALTER COLUMN createdby DROP DEFAULT;

//        ALTER TABLE {DatabaseConfig.Schema_Global}.{tableName}
//            ALTER COLUMN updatedby DROP DEFAULT;
//    END IF;
//END $$;
//""");
//    }
//}
