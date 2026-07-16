using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(103, "School template — employees")]
public sealed class S103_CreateEmployeesTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Global;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Exists())
        {
            Create.Table(DatabaseConfig.TableEmployees).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("firstname").AsString(50).NotNullable()
                .WithColumn("lastname").AsString(50).NotNullable()
                .WithColumn("dob").AsDate().NotNullable()
                .WithColumn("gender").AsString(20).NotNullable()
                .WithColumn("bloodgroup").AsString(10).Nullable()
                .WithColumn("aadhaarno").AsString(20).Nullable()
                .WithColumn("panno").AsString(20).Nullable()
                .WithColumn("mobile").AsString(20).NotNullable()
                .WithColumn("alternatemobile").AsString(20).Nullable()
                .WithColumn("email").AsString(256).NotNullable()
                .WithColumn("address").AsString(1000).Nullable()
                .WithColumn("employeeid").AsString(50).Nullable()
                .WithColumn("joiningdate").AsDate().NotNullable()
                .WithColumn("designation").AsString(100).Nullable()
                .WithColumn("experience").AsInt32().WithDefaultValue(0)
                .WithColumn("salarygrade").AsString(50).Nullable()
                .WithColumn("employmenttype").AsString(50).WithDefaultValue("Full-time")
                .WithColumn("qualifications").AsString(2000).Nullable()
                .WithColumn("bankaccountnumber").AsString(50).Nullable()
                .WithColumn("bankifsccode").AsString(20).Nullable()
                .WithColumn("bankname").AsString(50).Nullable()
                .WithColumn("classid").AsGuid().Nullable()
                    .ForeignKey("fk_employees_classid", S, DatabaseConfig.TableClasses, "id")
                .WithColumn("shiftstarttime").AsString(5).Nullable()
                .WithColumn("shiftendtime").AsString(5).Nullable()
                .WithColumn("usertypecode").AsString(50).NotNullable().WithDefaultValue("TEACHER")
                .WithColumn("portalrolename").AsString(100).NotNullable().WithDefaultValue("Teacher")
                .WithColumn("portalaccess").AsBoolean().WithDefaultValue(true)
                .WithColumn("username").AsString(100).Nullable()
                .WithColumn("userid").AsGuid().Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableEmployees}
    ADD CONSTRAINT fk_employees_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

ALTER TABLE {S}.{DatabaseConfig.TableEmployees}
    ADD CONSTRAINT fk_employees_user FOREIGN KEY (userid)
    REFERENCES {G}.{DatabaseConfig.TableUsers}(id) ON DELETE SET NULL;

CREATE UNIQUE INDEX ux_employees_employeeid_branch_active
    ON {S}.{DatabaseConfig.TableEmployees} (branchid, lower(employeeid))
    WHERE isactive = true AND employeeid IS NOT NULL AND btrim(employeeid) <> '';

CREATE UNIQUE INDEX ux_employees_username_active
    ON {S}.{DatabaseConfig.TableEmployees} (lower(username))
    WHERE isactive = true AND username IS NOT NULL AND btrim(username) <> '';

CREATE INDEX ix_employees_branchid ON {S}.{DatabaseConfig.TableEmployees} (branchid);
""");
        }
    }

    public override void Down()
    {
        Execute.Sql($"""
DROP INDEX IF EXISTS {S}.ux_employees_username_active;
DROP INDEX IF EXISTS {S}.ux_employees_employeeid_branch_active;
ALTER TABLE {S}.{DatabaseConfig.TableEmployees} DROP CONSTRAINT IF EXISTS fk_employees_user;
ALTER TABLE {S}.{DatabaseConfig.TableEmployees} DROP CONSTRAINT IF EXISTS fk_employees_branchid;
""");
        Delete.Table(DatabaseConfig.TableEmployees).InSchema(S);
    }
}
