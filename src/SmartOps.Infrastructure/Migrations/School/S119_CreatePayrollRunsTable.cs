using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(119, "School template — payrollruns")]
public sealed class S119_CreatePayrollRunsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string PayrollYearMonthUnique = "uq_payrollruns_year_month";

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TablePayrollRuns).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TablePayrollRuns).InSchema(S)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("payyear").AsInt32().NotNullable()
            .WithColumn("paymonth").AsInt32().NotNullable()
            .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
            .WithColumn("useattendancewisesalary").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("totalgross").AsDecimal(14, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("totaldeductions").AsDecimal(14, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("totalnet").AsDecimal(14, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("employeecount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("processedon").AsDateTime().Nullable()
            .WithAuditColumns();

        Create.UniqueConstraint(PayrollYearMonthUnique)
            .OnTable(DatabaseConfig.TablePayrollRuns).WithSchema(S)
            .Columns("payyear", "paymonth");
    }

    public override void Down() => Delete.Table(DatabaseConfig.TablePayrollRuns).InSchema(S);
}
