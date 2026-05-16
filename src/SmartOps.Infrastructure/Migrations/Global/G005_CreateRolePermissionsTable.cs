using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Migration(5, "Global — role menu permissions")]
public sealed class G005_CreateRoleMenuPermissionsTable : Migration
{
    public override void Up()
    {
        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableRoleMenuPermissions).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableRoleMenuPermissions).InSchema(DatabaseConfig.Schema_Global)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("roleid").AsGuid().NotNullable()
            .WithColumn("menuid").AsGuid().NotNullable()
            .WithColumn("canview").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("canadd").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("canedit").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("candelete").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("canexport").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Create.UniqueConstraint("uq_role_menu_permissions_role_menu")
            .OnTable(DatabaseConfig.TableRoleMenuPermissions)
            .WithSchema(DatabaseConfig.Schema_Global)
            .Columns("roleid", "menuid");

        Execute.Sql($"""
ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    ADD CONSTRAINT fk_role_menu_permissions_role FOREIGN KEY (roleid)
    REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}(id) ON DELETE CASCADE;
""");

        Execute.Sql($"""
ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    ADD CONSTRAINT fk_role_menu_permissions_menu FOREIGN KEY (menuid)
    REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}(id) ON DELETE CASCADE;
""");
    }

    public override void Down()
    {
        Execute.Sql($"ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} DROP CONSTRAINT IF EXISTS fk_role_menu_permissions_role;");
        Execute.Sql($"ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} DROP CONSTRAINT IF EXISTS fk_role_menu_permissions_menu;");
        Delete.UniqueConstraint("uq_role_menu_permissions_role_menu").FromTable(DatabaseConfig.TableRoleMenuPermissions).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableRoleMenuPermissions).InSchema(DatabaseConfig.Schema_Global);
    }
}
