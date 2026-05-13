using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(012)]
public sealed class M012_CreateAcademicYearsTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableAcademicYears).Exists())
        {
            Create.Table(DatabaseConfig.TableAcademicYears).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("title").AsString(50).NotNullable() // e.g., 2024-25
                .WithColumn("startdate").AsDate().NotNullable()
                .WithColumn("enddate").AsDate().NotNullable()
                .WithAuditColumns();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableAcademicYears).InSchema(DatabaseConfig.Schema_Global);
    }
}
