using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(121, "School template — payrollentrylines")]
public sealed class S121_CreatePayrollEntryLinesTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string PayrollEntryLineEntryIndex = "ix_payrollentrylines_entryid";

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TablePayrollEntryLines).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TablePayrollEntryLines).InSchema(S)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("payrollentryid").AsGuid().NotNullable()
                .ForeignKey("fk_payrollentrylines_payrollentryid", S, DatabaseConfig.TablePayrollEntries, "id")
            .WithColumn("salaryversioncomponentid").AsGuid().Nullable()
                .ForeignKey("fk_payrollentrylines_salaryversioncomponentid", S, DatabaseConfig.TableSalaryVersionComponents, "id")
            .WithColumn("componentname").AsString(200).NotNullable()
            .WithColumn("componenttype").AsInt16().NotNullable().WithDefaultValue(0)
            .WithColumn("amount").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("isearning").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithAuditColumns();

        Create.Index(PayrollEntryLineEntryIndex)
            .OnTable(DatabaseConfig.TablePayrollEntryLines).InSchema(S)
            .OnColumn("payrollentryid").Ascending();
    }

    public override void Down() => Delete.Table(DatabaseConfig.TablePayrollEntryLines).InSchema(S);
}
