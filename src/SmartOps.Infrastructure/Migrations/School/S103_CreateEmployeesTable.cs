using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(103, "School template — employees")]
public sealed class S103_CreateEmployeesTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Exists())
        {
            Create.Table(DatabaseConfig.TableEmployees).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
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
                .WithColumn("employeeid").AsString(50).Nullable().Unique()
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
                .WithColumn("username").AsString(100).Nullable().Unique()
                .WithColumn("userid").AsGuid().Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableEmployees}
    ADD CONSTRAINT fk_employees_user FOREIGN KEY (userid)
    REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}(id) ON DELETE SET NULL;
""");
        }
    }

    public override void Down()
    {
        Execute.Sql($"ALTER TABLE {S}.{DatabaseConfig.TableEmployees} DROP CONSTRAINT IF EXISTS fk_employees_user;");
        Delete.Table(DatabaseConfig.TableEmployees).InSchema(S);
    }
}
