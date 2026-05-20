using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(108, "School template — departments")]
public sealed class S108_CreateDepartmentsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableDepartments).Exists())
        {
            Create.Table(DatabaseConfig.TableDepartments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("code").AsString(50).NotNullable().Unique()
                .WithColumn("name").AsString(200).NotNullable()
                .WithAuditColumns();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableTeachers).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableTeachers).Column("departmentid").Exists())
        {
            Alter.Table(DatabaseConfig.TableTeachers).InSchema(S)
                .AddColumn("departmentid").AsGuid().Nullable()
                    .ForeignKey("fk_teachers_departmentid", S, DatabaseConfig.TableDepartments, "id");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableTeachers).Column("departmentid").Exists())
        {
            Execute.Sql($"ALTER TABLE {S}.{DatabaseConfig.TableTeachers} DROP CONSTRAINT IF EXISTS fk_teachers_departmentid;");
            Delete.Column("departmentid").FromTable(DatabaseConfig.TableTeachers).InSchema(S);
        }

        Delete.Table(DatabaseConfig.TableDepartments).InSchema(S);
    }
}
