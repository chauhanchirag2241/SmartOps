using FluentMigrator;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(111, "School template — nullable teacher on class-subject mappings")]
public sealed class S111_AlterClassSubjectTeacherMappingsNullableTeacher : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string T => DatabaseConfig.TableClassSubjectTeacherMappings;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(T).Exists())
        {
            return;
        }

        Execute.Sql($"""
ALTER TABLE {S}.{T} DROP CONSTRAINT IF EXISTS uq_classsubjectteachermappings;
""");

        Execute.Sql($"""
ALTER TABLE {S}.{T} ALTER COLUMN teacherid DROP NOT NULL;
""");

        Execute.Sql($"""
DROP INDEX IF EXISTS {S}.uq_cst_mappings_class_subject_year;
""");

        Execute.Sql($"""
CREATE UNIQUE INDEX uq_cst_mappings_class_subject_year
ON {S}.{T} (classid, subjectid, academicyearid)
WHERE isactive = true;
""");
    }

    public override void Down()
    {
        if (!Schema.Schema(S).Table(T).Exists())
        {
            return;
        }

        Execute.Sql($"""
DELETE FROM {S}.{T} WHERE teacherid IS NULL;
""");

        Execute.Sql($"""
DROP INDEX IF EXISTS {S}.uq_cst_mappings_class_subject_year;
""");

        Execute.Sql($"""
ALTER TABLE {S}.{T} ALTER COLUMN teacherid SET NOT NULL;
""");

        Execute.Sql($"""
ALTER TABLE {S}.{T}
    ADD CONSTRAINT uq_classsubjectteachermappings
    UNIQUE (classid, subjectid, teacherid, academicyearid);
""");
    }
}
