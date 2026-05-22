using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(102, "School template — subjects")]
public sealed class S102_CreateSubjectsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableSubjects).Exists())
        {
            Create.Table(DatabaseConfig.TableSubjects).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
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
        }
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableSubjects).InSchema(S);
}
