using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(120, "School template — payrollentries")]
public sealed class S120_CreatePayrollEntriesTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string PayrollEntryRunIndex = "ix_payrollentries_runid";
    private const string PayrollEntryTeacherIndex = "ix_payrollentries_teacherid";

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TablePayrollEntries).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TablePayrollEntries).InSchema(S)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("payrollrunid").AsGuid().NotNullable()
                .ForeignKey("fk_payrollentries_payrollrunid", S, DatabaseConfig.TablePayrollRuns, "id")
            .WithColumn("teacherid").AsGuid().NotNullable()
                .ForeignKey("fk_payrollentries_teacherid", S, DatabaseConfig.TableTeachers, "id")
            .WithColumn("basicsalary").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("grosssalary").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("totaldeductions").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("netsalary").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
            .WithColumn("workingdays").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("presentdays").AsInt32().NotNullable().WithDefaultValue(0)
            .WithAuditColumns();

        Create.Index(PayrollEntryRunIndex)
            .OnTable(DatabaseConfig.TablePayrollEntries).InSchema(S)
            .OnColumn("payrollrunid").Ascending();

        Create.Index(PayrollEntryTeacherIndex)
            .OnTable(DatabaseConfig.TablePayrollEntries).InSchema(S)
            .OnColumn("teacherid").Ascending();
    }

    public override void Down() => Delete.Table(DatabaseConfig.TablePayrollEntries).InSchema(S);
}
