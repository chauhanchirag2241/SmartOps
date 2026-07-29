using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(103, "School template — employees")]
public sealed class S103_CreateEmployeesTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Man;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Exists())
        {
            Create.Table(DatabaseConfig.TableEmployees).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("userid").AsGuid().NotNullable()
                .WithColumn("dob").AsDate().NotNullable()
                .WithColumn("gender").AsString(20).NotNullable()
                .WithColumn("bloodgroup").AsString(10).Nullable()
                .WithColumn("aadhaarno").AsString(20).Nullable()
                .WithColumn("panno").AsString(20).Nullable()
                .WithColumn("alternatemobile").AsString(20).Nullable()
                .WithColumn("address").AsString(1000).Nullable()
                .WithColumn("employeecode").AsString(50).Nullable()
                .WithColumn("joiningdate").AsDate().NotNullable()
                .WithColumn("designation").AsString(100).Nullable()
                .WithColumn("experience").AsInt32().WithDefaultValue(0)
                .WithColumn("salarygrade").AsString(50).Nullable()
                .WithColumn("employmenttype").AsString(50).WithDefaultValue("Full-time")
                .WithColumn("qualifications").AsString(2000).Nullable()
                .WithColumn("bankaccountnumber").AsString(50).Nullable()
                .WithColumn("bankifsccode").AsString(20).Nullable()
                .WithColumn("bankname").AsString(50).Nullable()
                .WithColumn("shiftstarttime").AsString(5).Nullable()
                .WithColumn("shiftendtime").AsString(5).Nullable()
                .WithColumn("portalrolename").AsString(100).NotNullable().WithDefaultValue("Teacher")
                .WithColumn("portalaccess").AsBoolean().WithDefaultValue(true)
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableEmployees}
    ADD CONSTRAINT fk_employees_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

ALTER TABLE {S}.{DatabaseConfig.TableEmployees}
    ADD CONSTRAINT fk_employees_user FOREIGN KEY (userid)
    REFERENCES {G}.{DatabaseConfig.TableUsers}(id);

CREATE UNIQUE INDEX ux_employees_employeecode_branch_active
    ON {S}.{DatabaseConfig.TableEmployees} (branchid, lower(employeecode))
    WHERE isactive = true AND employeecode IS NOT NULL AND btrim(employeecode) <> '';

CREATE UNIQUE INDEX ux_employees_userid_active
    ON {S}.{DatabaseConfig.TableEmployees} (userid)
    WHERE isactive = true;

CREATE INDEX ix_employees_branchid ON {S}.{DatabaseConfig.TableEmployees} (branchid);
""");
        }
    }

    public override void Down()
    {
        Execute.Sql($"""
DROP INDEX IF EXISTS {S}.ux_employees_userid_active;
DROP INDEX IF EXISTS {S}.ux_employees_employeecode_branch_active;
ALTER TABLE {S}.{DatabaseConfig.TableEmployees} DROP CONSTRAINT IF EXISTS fk_employees_user;
ALTER TABLE {S}.{DatabaseConfig.TableEmployees} DROP CONSTRAINT IF EXISTS fk_employees_branchid;
""");
        Delete.Table(DatabaseConfig.TableEmployees).InSchema(S);
    }
}
