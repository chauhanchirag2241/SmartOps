using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(006)]
public sealed class M006_CreateSchoolsTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Exists())
        {
            Create.Schema(DatabaseConfig.Schema_Global);
        }

        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableSchools).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableSchools).InSchema(DatabaseConfig.Schema_Global)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("subdomain").AsString(100).NotNullable().Unique()
            .WithAuditColumns();
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableSchools).InSchema(DatabaseConfig.Schema_Global);
    }
}
