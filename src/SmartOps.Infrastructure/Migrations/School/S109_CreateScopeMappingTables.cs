using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(109, "School template — class-subject-teacher, HOD, and staff scope tables")]
public sealed class S109_CreateScopeMappingTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Man;

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
                .WithColumn("employeeid").AsGuid().Nullable()
                    .ForeignKey("fk_cst_mappings_employeeid", S, DatabaseConfig.TableEmployees, "id")
                .WithColumn("academicyearid").AsGuid().NotNullable()
                    .ForeignKey("fk_cst_mappings_academicyearid", S, DatabaseConfig.TableAcademicYears, "id")
                .WithAuditColumns();

            Create.Index("ix_cst_mappings_employeeid")
                .OnTable(DatabaseConfig.TableClassSubjectTeacherMappings).InSchema(S)
                .OnColumn("employeeid").Ascending();

            Create.Index("ix_cst_mappings_classid")
                .OnTable(DatabaseConfig.TableClassSubjectTeacherMappings).InSchema(S)
                .OnColumn("classid").Ascending();

            Create.Index("ix_cst_mappings_class_year")
                .OnTable(DatabaseConfig.TableClassSubjectTeacherMappings).InSchema(S)
                .OnColumn("classid").Ascending()
                .OnColumn("academicyearid").Ascending();

            Create.Index("ix_cst_mappings_employee_year")
                .OnTable(DatabaseConfig.TableClassSubjectTeacherMappings).InSchema(S)
                .OnColumn("employeeid").Ascending()
                .OnColumn("academicyearid").Ascending();

            Execute.Sql($"""
CREATE UNIQUE INDEX uq_cst_mappings_class_subject_year
ON {S}.{DatabaseConfig.TableClassSubjectTeacherMappings} (classid, subjectid, academicyearid)
WHERE isactive = true;
""");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassSettings).Exists())
        {
            Create.Table(DatabaseConfig.TableClassSettings).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classgroupid").AsGuid().Nullable()
                    .ForeignKey("fk_classsettings_classgroupid", S, DatabaseConfig.TableClassGroups, "id")
                .WithColumn("sectionid").AsGuid().Nullable()
                    .ForeignKey("fk_classsettings_sectionid", S, DatabaseConfig.TableClasses, "id")
                .WithColumn("teacherid").AsGuid().Nullable()
                    .ForeignKey("fk_classsettings_teacherid", S, DatabaseConfig.TableEmployees, "id")
                .WithAuditColumns();

            Create.Index("ix_classsettings_sectionid")
                .OnTable(DatabaseConfig.TableClassSettings).InSchema(S)
                .OnColumn("sectionid").Ascending();

            Create.Index("ix_classsettings_teacherid")
                .OnTable(DatabaseConfig.TableClassSettings).InSchema(S)
                .OnColumn("teacherid").Ascending();

            Create.Index("ix_classsettings_classgroupid")
                .OnTable(DatabaseConfig.TableClassSettings).InSchema(S)
                .OnColumn("classgroupid").Ascending();

            // One active settings row per section (class) — class teacher and future class-wise settings.
            Execute.Sql($"""
CREATE UNIQUE INDEX uq_classsettings_section
ON {S}.{DatabaseConfig.TableClassSettings} (sectionid)
WHERE sectionid IS NOT NULL AND isactive = true;
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
        Delete.Table(DatabaseConfig.TableHodDepartmentAssignments).InSchema(S);
        Delete.Table(DatabaseConfig.TableClassSettings).InSchema(S);
        Delete.Table(DatabaseConfig.TableClassSubjectTeacherMappings).InSchema(S);
    }
}
