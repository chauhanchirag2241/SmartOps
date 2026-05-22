using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

/// <summary>
/// Ensures <c>studentcustomfields</c> exists on the school template (added to S104 after some DBs already migrated).</summary>
[Migration(113, "School template — student custom fields")]
public sealed class S113_CreateStudentCustomFieldsTable : Migration
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
