using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

/// <summary>
/// Ensures <c>school.studentcustomfields</c> exists on the platform template.
/// S104 may have already run before this table was added to that migration.
/// </summary>
[Tags("School")]
[Migration(128, "School template — ensure student custom fields table")]
public sealed class S128_EnsureStudentCustomFieldsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
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
        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentCustomFields).Exists())
        {
            Delete.Table(DatabaseConfig.TableStudentCustomFields).InSchema(S);
        }
    }
}
