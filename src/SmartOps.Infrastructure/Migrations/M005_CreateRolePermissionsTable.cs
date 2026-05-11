using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(005)]
public sealed class M005_CreateRolePermissionsTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Exists())
        {
            Create.Schema(DatabaseConfig.Schema_Global);
        }

        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableRolePermissions).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableRolePermissions).InSchema(DatabaseConfig.Schema_Global)
            .WithColumn("roleid").AsGuid().NotNullable()
            .WithColumn("permissionid").AsGuid().NotNullable()
            .WithAuditColumns();

        Create.PrimaryKey("pk_role_permissions")
            .OnTable(DatabaseConfig.TableRolePermissions)
            .WithSchema(DatabaseConfig.Schema_Global)
            .Columns("roleid", "permissionid");

        Execute.Sql($"""
ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions}
    ADD CONSTRAINT fk_role_permissions_role FOREIGN KEY (roleid) REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}(id) ON DELETE CASCADE;
""");

        Execute.Sql($"""
ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions}
    ADD CONSTRAINT fk_role_permissions_permission FOREIGN KEY (permissionid) REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TablePermissions}(id) ON DELETE CASCADE;
""");
    }

    public override void Down()
    {
        Execute.Sql($"ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions} DROP CONSTRAINT IF EXISTS fk_role_permissions_role;");
        Execute.Sql($"ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions} DROP CONSTRAINT IF EXISTS fk_role_permissions_permission;");

        Delete.PrimaryKey("pk_role_permissions").FromTable(DatabaseConfig.TableRolePermissions).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableRolePermissions).InSchema(DatabaseConfig.Schema_Global);
    }
}
