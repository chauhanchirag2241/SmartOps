using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(112, "School template — homework")]
public sealed class S112_CreateHomeworkTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string HomeworkStudentUnique = "uq_homeworkdetails_homework_student";
    private const string HomeworkClassIndex = "ix_homework_classid";
    private const string HomeworkDueIndex = "ix_homework_duedate";
    private const string HomeworkDetailsHomeworkIndex = "ix_homeworkdetails_homeworkid";

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableHomework).Exists())
        {
            Create.Table(DatabaseConfig.TableHomework).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classid").AsGuid().NotNullable()
                .WithColumn("subjectid").AsGuid().NotNullable()
                .WithColumn("employeeid").AsGuid().NotNullable()
                .WithColumn("title").AsString(300).NotNullable()
                .WithColumn("description").AsString(int.MaxValue).Nullable()
                .WithColumn("assigndate").AsDate().NotNullable()
                .WithColumn("duedate").AsDate().NotNullable()
                .WithColumn("priority").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("marks").AsInt32().Nullable()
                .WithColumn("submissiontype").AsInt16().NotNullable().WithDefaultValue(0)
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableHomeworkDetails).Exists())
        {
            Create.Table(DatabaseConfig.TableHomeworkDetails).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("homeworkid").AsGuid().NotNullable()
                .WithColumn("classid").AsGuid().NotNullable()
                .WithColumn("subjectid").AsGuid().NotNullable()
                .WithColumn("studentid").AsGuid().NotNullable()
                .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("submittedon").AsDate().Nullable()
                .WithColumn("marks").AsInt32().Nullable()
                .WithColumn("remark").AsString(500).Nullable()
                .WithAuditColumns();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableHomeworkDetails).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableHomeworkDetails).Constraint(HomeworkStudentUnique).Exists())
        {
            Create.UniqueConstraint(HomeworkStudentUnique)
                .OnTable(DatabaseConfig.TableHomeworkDetails).WithSchema(S)
                .Columns("homeworkid", "studentid");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableHomework).Index(HomeworkClassIndex).Exists())
        {
            Create.Index(HomeworkClassIndex)
                .OnTable(DatabaseConfig.TableHomework).InSchema(S)
                .OnColumn("classid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableHomework).Index(HomeworkDueIndex).Exists())
        {
            Create.Index(HomeworkDueIndex)
                .OnTable(DatabaseConfig.TableHomework).InSchema(S)
                .OnColumn("duedate").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableHomeworkDetails).Index(HomeworkDetailsHomeworkIndex).Exists())
        {
            Create.Index(HomeworkDetailsHomeworkIndex)
                .OnTable(DatabaseConfig.TableHomeworkDetails).InSchema(S)
                .OnColumn("homeworkid").Ascending();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableHomeworkDetails).InSchema(S);
        Delete.Table(DatabaseConfig.TableHomework).InSchema(S);
    }
}
