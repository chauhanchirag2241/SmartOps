using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(21, "Global — dashboard widgets and role widget permissions")]
public sealed class G021_CreateDashboardWidgetsTables : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableDashboardWidgets).Exists())
        {
            Create.Table(DatabaseConfig.TableDashboardWidgets).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("code").AsString(80).NotNullable()
                .WithColumn("name").AsString(120).NotNullable()
                .WithColumn("category").AsString(40).NotNullable()
                .WithColumn("requiredmenucode").AsString(80).NotNullable()
                .WithColumn("displayorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("defaultsize").AsString(20).NotNullable().WithDefaultValue("stat")
                .WithAuditColumns();

            Create.UniqueConstraint("uq_dashboard_widgets_code")
                .OnTable(DatabaseConfig.TableDashboardWidgets)
                .WithSchema(DatabaseConfig.Schema_Global)
                .Columns("code");
        }

        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableRoleDashboardWidgetPermissions).Exists())
        {
            Create.Table(DatabaseConfig.TableRoleDashboardWidgetPermissions).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("roleid").AsGuid().NotNullable()
                .WithColumn("widgetid").AsGuid().NotNullable()
                .WithColumn("canview").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithAuditColumns();

            Create.UniqueConstraint("uq_role_dashboard_widget_permissions_role_widget")
                .OnTable(DatabaseConfig.TableRoleDashboardWidgetPermissions)
                .WithSchema(DatabaseConfig.Schema_Global)
                .Columns("roleid", "widgetid");

            Execute.Sql($"""
ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleDashboardWidgetPermissions}
    ADD CONSTRAINT fk_role_dashboard_widget_permissions_role FOREIGN KEY (roleid)
    REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}(id) ON DELETE CASCADE;
""");

            Execute.Sql($"""
ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleDashboardWidgetPermissions}
    ADD CONSTRAINT fk_role_dashboard_widget_permissions_widget FOREIGN KEY (widgetid)
    REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableDashboardWidgets}(id) ON DELETE CASCADE;
""");
        }
    }

    public override void Down()
    {
        Execute.Sql($"ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleDashboardWidgetPermissions} DROP CONSTRAINT IF EXISTS fk_role_dashboard_widget_permissions_role;");
        Execute.Sql($"ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleDashboardWidgetPermissions} DROP CONSTRAINT IF EXISTS fk_role_dashboard_widget_permissions_widget;");
        Delete.UniqueConstraint("uq_role_dashboard_widget_permissions_role_widget").FromTable(DatabaseConfig.TableRoleDashboardWidgetPermissions).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableRoleDashboardWidgetPermissions).InSchema(DatabaseConfig.Schema_Global);
        Delete.UniqueConstraint("uq_dashboard_widgets_code").FromTable(DatabaseConfig.TableDashboardWidgets).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableDashboardWidgets).InSchema(DatabaseConfig.Schema_Global);
    }
}
