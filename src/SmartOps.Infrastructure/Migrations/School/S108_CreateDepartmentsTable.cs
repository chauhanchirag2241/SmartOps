using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(108, "School template — departments")]
public sealed class S108_CreateDepartmentsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Global;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableDepartments).Exists())
        {
            Create.Table(DatabaseConfig.TableDepartments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("code").AsString(50).NotNullable()
                .WithColumn("name").AsString(200).NotNullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableDepartments}
    ADD CONSTRAINT fk_departments_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE UNIQUE INDEX uq_departments_branch_code
    ON {S}.{DatabaseConfig.TableDepartments} (branchid, lower(code))
    WHERE isactive = true;

CREATE INDEX ix_departments_branchid ON {S}.{DatabaseConfig.TableDepartments} (branchid);
""");
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Column("departmentid").Exists())
        {
            Alter.Table(DatabaseConfig.TableEmployees).InSchema(S)
                .AddColumn("departmentid").AsGuid().Nullable()
                    .ForeignKey("fk_employees_departmentid", S, DatabaseConfig.TableDepartments, "id");
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Column("reportingmanagerid").Exists())
        {
            Alter.Table(DatabaseConfig.TableEmployees).InSchema(S)
                .AddColumn("reportingmanagerid").AsGuid().Nullable()
                    .ForeignKey("fk_employees_reportingmanagerid", S, DatabaseConfig.TableEmployees, "id");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Column("reportingmanagerid").Exists())
        {
            Execute.Sql($"ALTER TABLE {S}.{DatabaseConfig.TableEmployees} DROP CONSTRAINT IF EXISTS fk_employees_reportingmanagerid;");
            Delete.Column("reportingmanagerid").FromTable(DatabaseConfig.TableEmployees).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Column("departmentid").Exists())
        {
            Execute.Sql($"ALTER TABLE {S}.{DatabaseConfig.TableEmployees} DROP CONSTRAINT IF EXISTS fk_employees_departmentid;");
            Delete.Column("departmentid").FromTable(DatabaseConfig.TableEmployees).InSchema(S);
        }

        Delete.Table(DatabaseConfig.TableDepartments).InSchema(S);
    }
}
