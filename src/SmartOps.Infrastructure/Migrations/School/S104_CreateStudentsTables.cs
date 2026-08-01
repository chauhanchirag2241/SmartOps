using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
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
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("userid").AsGuid().NotNullable()
                .WithColumn("admissionno").AsString(50).Nullable()
                .WithColumn("middlename").AsString(50).Nullable()
                .WithColumn("dob").AsDate().Nullable()
                .WithColumn("gender").AsString(20).Nullable()
                .WithColumn("bloodgroup").AsString(10).Nullable()
                .WithColumn("aadhaarno").AsString(20).Nullable()
                .WithColumn("caste").AsString(100).Nullable()
                .WithColumn("category").AsString(50).Nullable()
                .WithColumn("address").AsString(1000).Nullable()
                .WithColumn("photourl").AsString(1000).Nullable()
                .WithColumn("remarks").AsString(1000).Nullable()
                .WithColumn("portalaccess").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableStudents}
    ADD CONSTRAINT fk_students_branchid FOREIGN KEY (branchid)
    REFERENCES {DatabaseConfig.Schema_Man}.{DatabaseConfig.TableSchoolBranches}(id);

ALTER TABLE {S}.{DatabaseConfig.TableStudents}
    ADD CONSTRAINT fk_students_user FOREIGN KEY (userid)
    REFERENCES {DatabaseConfig.Schema_Man}.{DatabaseConfig.TableUsers}(id);

CREATE UNIQUE INDEX IF NOT EXISTS ux_students_admissionno_branch_active_ci
    ON {S}.{DatabaseConfig.TableStudents} (branchid, lower(admissionno))
    WHERE isactive = true AND admissionno IS NOT NULL AND btrim(admissionno) <> '';

CREATE UNIQUE INDEX IF NOT EXISTS ux_students_userid_active
    ON {S}.{DatabaseConfig.TableStudents} (userid)
    WHERE isactive = true;

CREATE INDEX IF NOT EXISTS ix_students_branchid ON {S}.{DatabaseConfig.TableStudents} (branchid);
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
                .WithColumn("email").AsString(256).Nullable()
                .WithColumn("occupation").AsString(100).Nullable()
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableStudentAcademics).Exists())
        {
            Create.Table(DatabaseConfig.TableStudentAcademics).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable()
                    .ForeignKey("fk_studentacademics_studentid", S, DatabaseConfig.TableStudents, "id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("classgroupid").AsGuid().NotNullable()
                    .ForeignKey("fk_studentacademics_classgroupid", S, DatabaseConfig.TableClassGroups, "id")
                .WithColumn("classid").AsGuid().Nullable()
                    .ForeignKey("fk_studentacademics_classid", S, DatabaseConfig.TableClasses, "id")
                .WithColumn("admissiondate").AsDate().Nullable()
                .WithColumn("academicyearid").AsGuid().NotNullable()
                    .ForeignKey("fk_studentacademics_academicyearid", S, DatabaseConfig.TableAcademicYears, "id")
                .WithColumn("rollnumber").AsString(50).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
CREATE INDEX IF NOT EXISTS ix_studentacademics_classgroupid
    ON {S}.{DatabaseConfig.TableStudentAcademics} (classgroupid);
""");
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
                .WithColumn("tcnumber").AsString(255).Nullable()
                .WithAuditColumns();
        }

        EnsureStudentCustomFieldsTable();
    }

    private void EnsureStudentCustomFieldsTable()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableStudents).Exists())
        {
            return;
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentCustomFields).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableStudentCustomFields).InSchema(S)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("studentid").AsGuid().NotNullable()
                .ForeignKey("fk_studentcustomfields_studentid", S, DatabaseConfig.TableStudents, "id").OnDelete(System.Data.Rule.Cascade)
            .WithColumn("fieldlabel").AsString(255).NotNullable()
            .WithColumn("fieldvalue").AsString(1000).Nullable()
            .WithAuditColumns();
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableStudentCustomFields).InSchema(S);
        Delete.Table(DatabaseConfig.TableStudentPreviousSchools).InSchema(S);
        Delete.Table(DatabaseConfig.TableStudentAcademics).InSchema(S);
        Delete.Table(DatabaseConfig.TableStudentParents).InSchema(S);
        Execute.Sql($"""
            DROP INDEX IF EXISTS {S}.ix_students_branchid;
            DROP INDEX IF EXISTS {S}.ux_students_admissionno_branch_active_ci;
            ALTER TABLE {S}.{DatabaseConfig.TableStudents} DROP CONSTRAINT IF EXISTS fk_students_user;
            ALTER TABLE {S}.{DatabaseConfig.TableStudents} DROP CONSTRAINT IF EXISTS fk_students_branchid;
            """);
        Delete.Table(DatabaseConfig.TableStudents).InSchema(S);
    }
}
