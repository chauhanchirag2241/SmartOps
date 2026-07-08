using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(130, "School template - student documents")]
public sealed class S130_StudentDocumentsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableStudentDocuments).Exists())
        {
            Create.Table(DatabaseConfig.TableStudentDocuments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable()
                    .ForeignKey("fk_studentdocs_studentid", S, DatabaseConfig.TableStudents, "id").OnDelete(System.Data.Rule.Cascade)
                .WithColumn("documentname").AsString(255).NotNullable()
                .WithColumn("contenttype").AsString(100).NotNullable()
                .WithColumn("fileurl").AsString(1000).NotNullable()
                .WithAuditColumns();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableStudentDocuments).InSchema(S);
    }
}
