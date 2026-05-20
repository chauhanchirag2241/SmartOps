using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Migration(4, "Global — user roles")]
public sealed class G004_CreateUserRolesTable : Migration
{
    public override void Up()
    {
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
