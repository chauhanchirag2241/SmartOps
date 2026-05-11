using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(003)]
public sealed class M003_CreatePermissionsTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Exists())
        {
            Create.Schema(DatabaseConfig.Schema_Global);
        }

        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TablePermissions).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TablePermissions).InSchema(DatabaseConfig.Schema_Global)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("name").AsString(200).NotNullable().Unique()
            .WithColumn("description").AsCustom("text").Nullable()
            .WithAuditColumns();
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TablePermissions).InSchema(DatabaseConfig.Schema_Global);
    }
}
