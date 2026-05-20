using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

/// <summary>
/// Template schema <c>school</c> — cloned per tenant when a school is created.
/// </summary>
[Migration(100, "School template — schema and academic years")]
public sealed class S100_CreateSchoolTemplateSchema : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Exists())
        {
            Create.Schema(S);
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableAcademicYears).Exists())
        {
            Create.Table(DatabaseConfig.TableAcademicYears).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("title").AsString(50).NotNullable()
                .WithColumn("startdate").AsDate().NotNullable()
                .WithColumn("enddate").AsDate().NotNullable()
                .WithAuditColumns();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableAcademicYears).InSchema(S);
        Delete.Schema(S);
    }
}
