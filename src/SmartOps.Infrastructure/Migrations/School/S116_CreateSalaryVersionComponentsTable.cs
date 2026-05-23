using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(116, "School template — salaryversioncomponents")]
public sealed class S116_CreateSalaryVersionComponentsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string VersionComponentIndex = "ix_salaryversioncomponents_versionid";

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableSalaryVersionComponents).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableSalaryVersionComponents).InSchema(S)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("salarystructureversionid").AsGuid().NotNullable()
                .ForeignKey("fk_salaryversioncomponents_versionid", S, DatabaseConfig.TableSalaryStructureVersions, "id")
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("shortcode").AsString(20).Nullable()
            .WithColumn("componenttype").AsInt16().NotNullable().WithDefaultValue(0)
            .WithColumn("calculationtype").AsInt16().NotNullable().WithDefaultValue(0)
            .WithColumn("value").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("istaxable").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Create.Index(VersionComponentIndex)
            .OnTable(DatabaseConfig.TableSalaryVersionComponents).InSchema(S)
            .OnColumn("salarystructureversionid").Ascending();
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableSalaryVersionComponents).InSchema(S);
}
