using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.Global;

/// <summary>
/// Platform catalog: dashboard widgets only.
/// Role widget permissions live only on each school DB (<c>man.roledashboardwidgetpermissions</c>).
/// </summary>
[Tags("Global")]
[Migration(19, "Global — dashboard widgets catalog")]
public sealed class G019_CreateDashboardWidgetsTables : Migration
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
    }

    public override void Down()
    {
        Delete.UniqueConstraint("uq_dashboard_widgets_code").FromTable(DatabaseConfig.TableDashboardWidgets).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableDashboardWidgets).InSchema(DatabaseConfig.Schema_Global);
    }
}
