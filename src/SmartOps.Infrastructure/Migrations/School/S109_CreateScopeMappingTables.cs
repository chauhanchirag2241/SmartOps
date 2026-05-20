using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(109, "School template — scope mapping tables")]
public sealed class S109_CreateScopeMappingTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Global;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassSubjectTeacherMappings).Exists())
        {
            Create.Table(DatabaseConfig.TableClassSubjectTeacherMappings).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classid").AsGuid().NotNullable()
                    .ForeignKey("fk_cst_mappings_classid", S, DatabaseConfig.TableClasses, "id")
                .WithColumn("subjectid").AsGuid().NotNullable()
                    .ForeignKey("fk_cst_mappings_subjectid", S, DatabaseConfig.TableSubjects, "id")
                .WithColumn("teacherid").AsGuid().NotNullable()
                    .ForeignKey("fk_cst_mappings_teacherid", S, DatabaseConfig.TableTeachers, "id")
                .WithColumn("academicyearid").AsGuid().NotNullable()
                    .ForeignKey("fk_cst_mappings_academicyearid", S, DatabaseConfig.TableAcademicYears, "id")
                .WithColumn("isclassteacher").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithAuditColumns();

            Create.UniqueConstraint("uq_classsubjectteachermappings")
                .OnTable(DatabaseConfig.TableClassSubjectTeacherMappings).WithSchema(S)
                .Columns("classid", "subjectid", "teacherid", "academicyearid");

            Create.Index("ix_cst_mappings_teacherid")
                .OnTable(DatabaseConfig.TableClassSubjectTeacherMappings).InSchema(S)
                .OnColumn("teacherid").Ascending();

            Create.Index("ix_cst_mappings_classid")
                .OnTable(DatabaseConfig.TableClassSubjectTeacherMappings).InSchema(S)
                .OnColumn("classid").Ascending();

            Create.Index("ix_cst_mappings_class_year")
                .OnTable(DatabaseConfig.TableClassSubjectTeacherMappings).InSchema(S)
                .OnColumn("classid").Ascending()
                .OnColumn("academicyearid").Ascending();

            Create.Index("ix_cst_mappings_teacher_year")
                .OnTable(DatabaseConfig.TableClassSubjectTeacherMappings).InSchema(S)
                .OnColumn("teacherid").Ascending()
                .OnColumn("academicyearid").Ascending();

            Execute.Sql($"""
CREATE UNIQUE INDEX uq_cst_mappings_one_class_teacher
ON {S}.{DatabaseConfig.TableClassSubjectTeacherMappings} (classid, academicyearid)
WHERE isclassteacher = true AND isactive = true;
""");
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
        Delete.Table(DatabaseConfig.TableClassSubjectTeacherMappings).InSchema(S);
    }
}
