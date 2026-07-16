using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(102, "School template — subjects")]
public sealed class S102_CreateSubjectsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Global;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableSubjects).Exists())
        {
            Create.Table(DatabaseConfig.TableSubjects).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("subjectname").AsString(100).NotNullable()
                .WithColumn("subjectcode").AsString(50).NotNullable()
                .WithColumn("subjecttype").AsInt32().Nullable()
                .WithColumn("subjectcategory").AsInt32().Nullable()
                .WithColumn("medium").AsInt32().Nullable()
                .WithColumn("assignedclasses").AsString(int.MaxValue).NotNullable().WithDefaultValue("[]")
                .WithColumn("periodsperweek").AsInt32().NotNullable()
                .WithColumn("periodduration").AsString(50).NotNullable()
                .WithColumn("teachingdays").AsString(int.MaxValue).NotNullable().WithDefaultValue("[]")
                .WithColumn("maxtheory").AsInt32().NotNullable()
                .WithColumn("maxpractical").AsInt32().NotNullable()
                .WithColumn("passingmarks").AsInt32().NotNullable()
                .WithColumn("gradesystem").AsInt32().NotNullable()
                .WithColumn("syllabustextbook").AsString(200).Nullable()
                .WithColumn("curriculum").AsInt32().NotNullable()
                .WithColumn("description").AsString(500).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableSubjects}
    ADD CONSTRAINT fk_subjects_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE UNIQUE INDEX uq_subjects_branch_code
    ON {S}.{DatabaseConfig.TableSubjects} (branchid, lower(subjectcode))
    WHERE isactive = true;

CREATE INDEX ix_subjects_branchid ON {S}.{DatabaseConfig.TableSubjects} (branchid);
""");
        }
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableSubjects).InSchema(S);
}
