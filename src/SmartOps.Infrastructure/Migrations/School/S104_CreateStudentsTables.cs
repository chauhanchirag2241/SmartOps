using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(104, "School template — students")]
public sealed class S104_CreateStudentsTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableStudents).Exists())
        {
            Create.Table(DatabaseConfig.TableStudents).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("admissionno").AsString(50).Nullable().Unique()
                .WithColumn("firstname").AsString(100).NotNullable()
                .WithColumn("middlename").AsString(100).Nullable()
                .WithColumn("lastname").AsString(100).NotNullable()
                .WithColumn("dob").AsDate().Nullable()
                .WithColumn("gender").AsString(20).Nullable()
                .WithColumn("bloodgroup").AsString(10).Nullable()
                .WithColumn("mobile").AsString(20).Nullable()
                .WithColumn("email").AsString(256).Nullable()
                .WithColumn("aadhaarno").AsString(20).Nullable()
                .WithColumn("address").AsString(1000).Nullable()
                .WithColumn("photourl").AsString(1000).Nullable()
                .WithColumn("remarks").AsString(1000).Nullable()
                .WithColumn("portalaccess").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("userid").AsGuid().Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableStudents}
    ADD CONSTRAINT fk_students_user FOREIGN KEY (userid)
    REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}(id) ON DELETE SET NULL;
""");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableStudentParents).Exists())
        {
            Create.Table(DatabaseConfig.TableStudentParents).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable()
                    .ForeignKey("fk_studentparents_studentid", S, DatabaseConfig.TableStudents, "id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("relationtype").AsString(50).NotNullable()
                .WithColumn("name").AsString(100).NotNullable()
                .WithColumn("mobile").AsString(20).Nullable()
                .WithColumn("occupation").AsString(100).Nullable()
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableStudentAcademics).Exists())
        {
            Create.Table(DatabaseConfig.TableStudentAcademics).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable()
                    .ForeignKey("fk_studentacademics_studentid", S, DatabaseConfig.TableStudents, "id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("classid").AsGuid().NotNullable()
                    .ForeignKey("fk_studentacademics_classid", S, DatabaseConfig.TableClasses, "id")
                .WithColumn("admissiondate").AsDate().Nullable()
                .WithColumn("academicyearid").AsGuid().NotNullable()
                    .ForeignKey("fk_studentacademics_academicyearid", S, DatabaseConfig.TableAcademicYears, "id")
                .WithColumn("rollnumber").AsString(50).Nullable()
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableStudentPreviousSchools).Exists())
        {
            Create.Table(DatabaseConfig.TableStudentPreviousSchools).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable()
                    .ForeignKey("fk_studentprevschools_studentid", S, DatabaseConfig.TableStudents, "id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("schoolname").AsString(255).Nullable()
                .WithColumn("lastclasspassed").AsString(50).Nullable()
                .WithColumn("percentageorcgpa").AsString(50).Nullable()
                .WithColumn("tcnumber").AsString(100).Nullable()
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeConfigs).Exists())
        {
            Create.Table(DatabaseConfig.TableStudentFeeConfigs).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable()
                    .ForeignKey("fk_studentfeeconfigs_studentid", S, DatabaseConfig.TableStudents, "id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("discounttype").AsString(100).Nullable()
                .WithColumn("discountvalue").AsDecimal(18, 2).Nullable()
                .WithColumn("ispercentage").AsBoolean().Nullable()
                .WithColumn("discountremarks").AsString(500).Nullable()
                .WithColumn("paymentmode").AsString(50).Nullable()
                .WithColumn("firstduedate").AsDate().Nullable()
                .WithAuditColumns();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableStudentFeeConfigs).InSchema(S);
        Delete.Table(DatabaseConfig.TableStudentPreviousSchools).InSchema(S);
        Delete.Table(DatabaseConfig.TableStudentAcademics).InSchema(S);
        Delete.Table(DatabaseConfig.TableStudentParents).InSchema(S);
        Execute.Sql($"ALTER TABLE {S}.{DatabaseConfig.TableStudents} DROP CONSTRAINT IF EXISTS fk_students_user;");
        Delete.Table(DatabaseConfig.TableStudents).InSchema(S);
    }
}
