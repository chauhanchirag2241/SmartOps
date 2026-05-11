using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(004)]
public sealed class M004_CreateUserRolesTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Exists())
        {
            Create.Schema(DatabaseConfig.Schema_Global);
        }

        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableUserRoles).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUserRoles).InSchema(DatabaseConfig.Schema_Global)
            .WithColumn("userid").AsGuid().NotNullable()
            .WithColumn("roleid").AsGuid().NotNullable()
            .WithAuditColumns();

        Create.PrimaryKey("pk_user_roles")
            .OnTable(DatabaseConfig.TableUserRoles)
            .WithSchema(DatabaseConfig.Schema_Global)
            .Columns("userid", "roleid");

        Execute.Sql($"""
ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles}
    ADD CONSTRAINT fk_user_roles_user FOREIGN KEY (userid) REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;
""");

        Execute.Sql($"""
ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles}
    ADD CONSTRAINT fk_user_roles_role FOREIGN KEY (roleid) REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}(id) ON DELETE CASCADE;
""");
    }

    public override void Down()
    {
        Execute.Sql($"ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles} DROP CONSTRAINT IF EXISTS fk_user_roles_user;");
        Execute.Sql($"ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles} DROP CONSTRAINT IF EXISTS fk_user_roles_role;");

        Delete.PrimaryKey("pk_user_roles").FromTable(DatabaseConfig.TableUserRoles).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableUserRoles).InSchema(DatabaseConfig.Schema_Global);
    }
}
