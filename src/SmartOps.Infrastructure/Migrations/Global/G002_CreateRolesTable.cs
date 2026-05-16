using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Migration(2, "Global — roles")]
public sealed class G002_CreateRolesTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableRoles).Exists())
        {
            Create.Table(DatabaseConfig.TableRoles).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("name").AsString(100).NotNullable().Unique()
                .WithColumn("description").AsCustom("text").Nullable()
                .WithAuditColumns();
        }
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableRoles).InSchema(DatabaseConfig.Schema_Global);
}
