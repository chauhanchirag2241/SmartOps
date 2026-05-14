using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(014)]
public sealed class M014_CreateTeachersTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableTeachers).Exists())
        {
            Create.Table(DatabaseConfig.TableTeachers).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("firstname").AsString(100).NotNullable()
                .WithColumn("lastname").AsString(100).NotNullable()
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
                .WithColumn("department").AsString(100).NotNullable()
                .WithColumn("designation").AsString(100).NotNullable()
                .WithColumn("experience").AsInt32().WithDefaultValue(0)
                .WithColumn("salarygrade").AsString(50).Nullable()
                .WithColumn("employmenttype").AsString(50).WithDefaultValue("Full-time")
                .WithColumn("qualifications").AsString(2000).Nullable() // Storing as semi-colon separated or JSON for simplicity in this demo
                .WithColumn("bankaccountnumber").AsString(50).Nullable()
                .WithColumn("bankifsccode").AsString(20).Nullable()
                .WithColumn("bankname").AsString(100).Nullable()
                .WithColumn("shift").AsString(100).Nullable()
                .WithColumn("weeklyperiods").AsInt32().WithDefaultValue(0)
                .WithColumn("maxperiodsperday").AsInt32().WithDefaultValue(0)
                .WithColumn("role").AsString(50).NotNullable().WithDefaultValue("Teacher")
                .WithColumn("portalaccess").AsBoolean().WithDefaultValue(true)
                .WithColumn("username").AsString(100).Nullable().Unique()
                .WithColumn("isactive").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithAuditColumns();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableTeachers).InSchema(DatabaseConfig.Schema_Global);
    }
}
