using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(103, "School template — teachers")]
public sealed class S103_CreateTeachersTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableTeachers).Exists())
        {
            Create.Table(DatabaseConfig.TableTeachers).InSchema(S)
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
                .WithColumn("qualifications").AsString(2000).Nullable()
                .WithColumn("bankaccountnumber").AsString(50).Nullable()
                .WithColumn("bankifsccode").AsString(20).Nullable()
                .WithColumn("bankname").AsString(100).Nullable()
                .WithColumn("classid").AsGuid().Nullable()
                    .ForeignKey("fk_teachers_classid", S, DatabaseConfig.TableClasses, "id")
                .WithColumn("shift").AsString(100).Nullable()
                .WithColumn("weeklyperiods").AsInt32().WithDefaultValue(0)
                .WithColumn("maxperiodsperday").AsInt32().WithDefaultValue(0)
                .WithColumn("role").AsString(50).NotNullable().WithDefaultValue("Teacher")
                .WithColumn("portalaccess").AsBoolean().WithDefaultValue(true)
                .WithColumn("username").AsString(100).Nullable().Unique()
                .WithColumn("userid").AsGuid().Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableTeachers}
    ADD CONSTRAINT fk_teachers_user FOREIGN KEY (userid)
    REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}(id) ON DELETE SET NULL;
""");
        }
    }

    public override void Down()
    {
        Execute.Sql($"ALTER TABLE {S}.{DatabaseConfig.TableTeachers} DROP CONSTRAINT IF EXISTS fk_teachers_user;");
        Delete.Table(DatabaseConfig.TableTeachers).InSchema(S);
    }
}
