using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(015)]
public class M015_CreateSubjectsTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableSubjects).Exists())
        {
            Create.Table(DatabaseConfig.TableSubjects).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey()
                .WithColumn("subjectname").AsString(100).NotNullable()
                .WithColumn("subjectcode").AsString(50).NotNullable()
                .WithColumn("subjecttype").AsInt32().NotNullable()
                .WithColumn("subjectcategory").AsInt32().NotNullable()
                .WithColumn("medium").AsInt32().NotNullable()
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
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableSubjects).InSchema(DatabaseConfig.Schema_Global);
    }
}
