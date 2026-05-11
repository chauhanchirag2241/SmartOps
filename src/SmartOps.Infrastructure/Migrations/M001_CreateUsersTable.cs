using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(001)]
public sealed class M001_CreateUsersTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Exists())
        {
            Create.Schema(DatabaseConfig.Schema_Global);
        }

        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableUsers).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("username").AsString(100).NotNullable().Unique()
            .WithColumn("email").AsString(256).NotNullable().Unique()
            .WithColumn("passwordhash").AsCustom("text").NotNullable()
            .WithAuditColumns();
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global);
    }
}
