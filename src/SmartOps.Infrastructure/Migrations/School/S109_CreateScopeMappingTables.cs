using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(109, "School template — scope mapping tables")]
public sealed class S109_CreateScopeMappingTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Global;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableTeacherClassAssignments).Exists())
        {
            Create.Table(DatabaseConfig.TableTeacherClassAssignments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("teacherid").AsGuid().NotNullable()
                    .ForeignKey("fk_teacherclassassignments_teacherid", S, DatabaseConfig.TableTeachers, "id")
                .WithColumn("classid").AsGuid().NotNullable()
                    .ForeignKey("fk_teacherclassassignments_classid", S, DatabaseConfig.TableClasses, "id")
                .WithColumn("academicyearid").AsGuid().NotNullable()
                    .ForeignKey("fk_teacherclassassignments_academicyearid", S, DatabaseConfig.TableAcademicYears, "id")
                .WithColumn("isclassteacher").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithAuditColumns();

            Create.UniqueConstraint("uq_teacherclassassignments")
                .OnTable(DatabaseConfig.TableTeacherClassAssignments).WithSchema(S)
                .Columns("teacherid", "classid", "academicyearid");

            Create.Index("ix_teacherclassassignments_teacherid")
                .OnTable(DatabaseConfig.TableTeacherClassAssignments).InSchema(S)
                .OnColumn("teacherid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableTeacherSubjectAssignments).Exists())
        {
            Create.Table(DatabaseConfig.TableTeacherSubjectAssignments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("teacherid").AsGuid().NotNullable()
                    .ForeignKey("fk_teachersubjectassignments_teacherid", S, DatabaseConfig.TableTeachers, "id")
                .WithColumn("subjectid").AsGuid().NotNullable()
                    .ForeignKey("fk_teachersubjectassignments_subjectid", S, DatabaseConfig.TableSubjects, "id")
                .WithColumn("classid").AsGuid().NotNullable()
                    .ForeignKey("fk_teachersubjectassignments_classid", S, DatabaseConfig.TableClasses, "id")
                .WithColumn("academicyearid").AsGuid().NotNullable()
                    .ForeignKey("fk_teachersubjectassignments_academicyearid", S, DatabaseConfig.TableAcademicYears, "id")
                .WithAuditColumns();

            Create.UniqueConstraint("uq_teachersubjectassignments")
                .OnTable(DatabaseConfig.TableTeacherSubjectAssignments).WithSchema(S)
                .Columns("teacherid", "subjectid", "classid", "academicyearid");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableHodDepartmentAssignments).Exists())
        {
            Create.Table(DatabaseConfig.TableHodDepartmentAssignments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("userid").AsGuid().NotNullable()
                .WithColumn("departmentid").AsGuid().NotNullable()
                    .ForeignKey("fk_hoddepartmentassignments_departmentid", S, DatabaseConfig.TableDepartments, "id")
                .WithColumn("academicyearid").AsGuid().Nullable()
                    .ForeignKey("fk_hoddepartmentassignments_academicyearid", S, DatabaseConfig.TableAcademicYears, "id")
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableHodDepartmentAssignments}
    ADD CONSTRAINT fk_hoddepartmentassignments_userid FOREIGN KEY (userid)
    REFERENCES {G}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;
""");

            Create.UniqueConstraint("uq_hoddepartmentassignments")
                .OnTable(DatabaseConfig.TableHodDepartmentAssignments).WithSchema(S)
                .Columns("userid", "departmentid");

            Create.Index("ix_hoddepartmentassignments_userid")
                .OnTable(DatabaseConfig.TableHodDepartmentAssignments).InSchema(S)
                .OnColumn("userid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableParentStudentMappings).Exists())
        {
            Create.Table(DatabaseConfig.TableParentStudentMappings).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("parentuserid").AsGuid().NotNullable()
                .WithColumn("studentid").AsGuid().NotNullable()
                    .ForeignKey("fk_parentstudentmappings_studentid", S, DatabaseConfig.TableStudents, "id")
                .WithColumn("relationtype").AsString(50).NotNullable().WithDefaultValue("Parent")
                .WithColumn("isprimary").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableParentStudentMappings}
    ADD CONSTRAINT fk_parentstudentmappings_parentuserid FOREIGN KEY (parentuserid)
    REFERENCES {G}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;
""");

            Create.UniqueConstraint("uq_parentstudentmappings")
                .OnTable(DatabaseConfig.TableParentStudentMappings).WithSchema(S)
                .Columns("parentuserid", "studentid");

            Create.Index("ix_parentstudentmappings_parentuserid")
                .OnTable(DatabaseConfig.TableParentStudentMappings).InSchema(S)
                .OnColumn("parentuserid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableStaffScopeAssignments).Exists())
        {
            Create.Table(DatabaseConfig.TableStaffScopeAssignments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("userid").AsGuid().NotNullable()
                .WithColumn("scopetype").AsString(50).NotNullable()
                .WithColumn("scopevalue").AsGuid().NotNullable()
                .WithColumn("modulecode").AsString(50).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableStaffScopeAssignments}
    ADD CONSTRAINT fk_staffscopeassignments_userid FOREIGN KEY (userid)
    REFERENCES {G}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;
""");

            Create.Index("ix_staffscopeassignments_userid")
                .OnTable(DatabaseConfig.TableStaffScopeAssignments).InSchema(S)
                .OnColumn("userid").Ascending();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableStaffScopeAssignments).InSchema(S);
        Delete.Table(DatabaseConfig.TableParentStudentMappings).InSchema(S);
        Delete.Table(DatabaseConfig.TableHodDepartmentAssignments).InSchema(S);
        Delete.Table(DatabaseConfig.TableTeacherSubjectAssignments).InSchema(S);
        Delete.Table(DatabaseConfig.TableTeacherClassAssignments).InSchema(S);
    }
}
