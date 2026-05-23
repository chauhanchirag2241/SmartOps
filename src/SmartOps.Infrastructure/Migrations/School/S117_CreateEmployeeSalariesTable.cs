using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(117, "School template — employeesalaries")]
public sealed class S117_CreateEmployeeSalariesTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string EmployeeSalaryActiveTeacherIndex = "uq_employeesalaries_active_teacher";

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployeeSalaries).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableEmployeeSalaries).InSchema(S)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("teacherid").AsGuid().NotNullable()
                .ForeignKey("fk_employeesalaries_teacherid", S, DatabaseConfig.TableTeachers, "id")
            .WithColumn("salarystructureversionid").AsGuid().NotNullable()
                .ForeignKey("fk_employeesalaries_salarystructureversionid", S, DatabaseConfig.TableSalaryStructureVersions, "id")
            .WithColumn("effectivedate").AsDate().NotNullable()
            .WithAuditColumns();

        Execute.Sql($"""
CREATE UNIQUE INDEX IF NOT EXISTS {EmployeeSalaryActiveTeacherIndex}
ON {S}.{DatabaseConfig.TableEmployeeSalaries} (teacherid)
WHERE isactive = true;
""");
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableEmployeeSalaries).InSchema(S);
}
