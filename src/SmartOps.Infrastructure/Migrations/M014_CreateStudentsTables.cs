using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(014)]
public sealed class M014_CreateStudentsTables : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Exists())
        {
            Create.Schema(DatabaseConfig.Schema_Global);
        }

        // 1. Students Table
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableStudents).Exists())
        {
            Create.Table(DatabaseConfig.TableStudents).InSchema(DatabaseConfig.Schema_Global)
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
                //.WithColumn("status").AsString(50).NotNullable().WithDefaultValue("Draft")
                .WithColumn("remarks").AsString(1000).Nullable()
                .WithAuditColumns();
        }

        // 2. Student Parents Table
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableStudentParents).Exists())
        {
            Create.Table(DatabaseConfig.TableStudentParents).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable().ForeignKey("fk_studentparents_studentid", DatabaseConfig.Schema_Global, DatabaseConfig.TableStudents, "id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("relationtype").AsString(50).NotNullable() // Father, Mother
                .WithColumn("name").AsString(100).NotNullable()
                .WithColumn("mobile").AsString(20).Nullable()
                .WithColumn("occupation").AsString(100).Nullable()
                .WithAuditColumns();
        }

        // 3. Student Academics Table
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableStudentAcademics).Exists())
        {
            Create.Table(DatabaseConfig.TableStudentAcademics).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable().ForeignKey("fk_studentacademics_studentid", DatabaseConfig.Schema_Global, DatabaseConfig.TableStudents, "id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("classid").AsGuid().NotNullable().ForeignKey("fk_studentacademics_classid", DatabaseConfig.Schema_Global, DatabaseConfig.TableClasses, "id")
                .WithColumn("admissiondate").AsDate().Nullable()
                .WithColumn("academicyearid").AsGuid().NotNullable().ForeignKey("fk_studentacademics_academicyearid", DatabaseConfig.Schema_Global, DatabaseConfig.TableAcademicYears, "id")
                .WithColumn("rollnumber").AsString(50).Nullable()
                .WithAuditColumns();
        }

        // 4. Student Previous Schools Table
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableStudentPreviousSchools).Exists())
        {
            Create.Table(DatabaseConfig.TableStudentPreviousSchools).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable().ForeignKey("fk_studentprevschools_studentid", DatabaseConfig.Schema_Global, DatabaseConfig.TableStudents, "id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("schoolname").AsString(255).Nullable()
                .WithColumn("lastclasspassed").AsString(50).Nullable()
                .WithColumn("percentageorcgpa").AsString(50).Nullable()
                .WithColumn("tcnumber").AsString(100).Nullable()
                .WithAuditColumns();
        }

        // 5. Student Fee Configs Table
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableStudentFeeConfigs).Exists())
        {
            Create.Table(DatabaseConfig.TableStudentFeeConfigs).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable().ForeignKey("fk_studentfeeconfigs_studentid", DatabaseConfig.Schema_Global, DatabaseConfig.TableStudents, "id").OnDelete(System.Data.Rule.Cascade)
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
        Delete.Table(DatabaseConfig.TableStudentFeeConfigs).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableStudentPreviousSchools).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableStudentAcademics).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableStudentParents).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableStudents).InSchema(DatabaseConfig.Schema_Global);
    }
}
