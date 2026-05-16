using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Migration(3, "Global — menus")]
public sealed class G003_CreateMenusTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableMenus).Exists())
        {
            Create.Table(DatabaseConfig.TableMenus).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("name").AsString(150).NotNullable()
                .WithColumn("code").AsString(50).NotNullable().Unique()
                .WithColumn("parentmenuid").AsGuid().Nullable()
                .WithColumn("route").AsString(300).Nullable()
                .WithColumn("icon").AsString(100).Nullable()
                .WithColumn("displayorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("application").AsString(20).NotNullable().WithDefaultValue("COMMON")
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    ADD CONSTRAINT fk_menus_parent FOREIGN KEY (parentmenuid)
    REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}(id) ON DELETE SET NULL;
""");
        }
    }

    public override void Down()
    {
        Execute.Sql($"ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} DROP CONSTRAINT IF EXISTS fk_menus_parent;");
        Delete.Table(DatabaseConfig.TableMenus).InSchema(DatabaseConfig.Schema_Global);
    }
}
