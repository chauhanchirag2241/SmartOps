using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(118, "School template — employeesalarycomponents")]
public sealed class S118_CreateEmployeeSalaryComponentsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string EmployeeSalaryComponentUnique = "uq_employeesalarycomponents_assignment_version_component";

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployeeSalaryComponents).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableEmployeeSalaryComponents).InSchema(S)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("employeesalaryid").AsGuid().NotNullable()
                .ForeignKey("fk_employeesalarycomponents_employeesalaryid", S, DatabaseConfig.TableEmployeeSalaries, "id")
            .WithColumn("salaryversioncomponentid").AsGuid().NotNullable()
                .ForeignKey("fk_employeesalarycomponents_salaryversioncomponentid", S, DatabaseConfig.TableSalaryVersionComponents, "id")
            .WithColumn("value").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
            .WithAuditColumns();

        Create.UniqueConstraint(EmployeeSalaryComponentUnique)
            .OnTable(DatabaseConfig.TableEmployeeSalaryComponents).WithSchema(S)
            .Columns("employeesalaryid", "salaryversioncomponentid");
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableEmployeeSalaryComponents).InSchema(S);
}
