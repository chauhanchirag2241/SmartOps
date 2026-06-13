using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(108, "School template — departments")]
public sealed class S108_CreateDepartmentsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private static readonly (Guid Id, string Code, string Name)[] DefaultDepartments =
    [
        (Guid.Parse("40000000-0000-0000-0000-000000000001"), "ACADEMICS", "Academics"),
        (Guid.Parse("40000000-0000-0000-0000-000000000002"), "ACCOUNTS", "Accounts"),
        (Guid.Parse("40000000-0000-0000-0000-000000000003"), "ADMIN", "Administration"),
        (Guid.Parse("40000000-0000-0000-0000-000000000004"), "HR", "Human Resources"),
        (Guid.Parse("40000000-0000-0000-0000-000000000005"), "NON_ACADEMIC_STAFF", "Non Academic Staff"),
    ];

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

        DateTimeOffset now = DateTimeOffset.UtcNow;

        Execute.Sql($"""
UPDATE {S}.{DatabaseConfig.TableDepartments}
SET code = 'NON_ACADEMIC_STAFF', name = 'Non Academic Staff', updatedon = '{now:O}', updatedby = '{SeedActor}'
WHERE code = 'IT';
""");

        foreach ((Guid id, string code, string name) in DefaultDepartments)
        {
            Execute.Sql($"""
INSERT INTO {S}.{DatabaseConfig.TableDepartments}
    (id, code, name, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{code}', '{name}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (SELECT 1 FROM {S}.{DatabaseConfig.TableDepartments} WHERE code = '{code}');
""");
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Column("departmentid").Exists())
        {
            Alter.Table(DatabaseConfig.TableEmployees).InSchema(S)
                .AddColumn("departmentid").AsGuid().Nullable()
                    .ForeignKey("fk_employees_departmentid", S, DatabaseConfig.TableDepartments, "id");
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Column("reportingmanagerid").Exists())
        {
            Alter.Table(DatabaseConfig.TableEmployees).InSchema(S)
                .AddColumn("reportingmanagerid").AsGuid().Nullable()
                    .ForeignKey("fk_employees_reportingmanagerid", S, DatabaseConfig.TableEmployees, "id");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Column("reportingmanagerid").Exists())
        {
            Execute.Sql($"ALTER TABLE {S}.{DatabaseConfig.TableEmployees} DROP CONSTRAINT IF EXISTS fk_employees_reportingmanagerid;");
            Delete.Column("reportingmanagerid").FromTable(DatabaseConfig.TableEmployees).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Column("departmentid").Exists())
        {
            Execute.Sql($"ALTER TABLE {S}.{DatabaseConfig.TableEmployees} DROP CONSTRAINT IF EXISTS fk_employees_departmentid;");
            Delete.Column("departmentid").FromTable(DatabaseConfig.TableEmployees).InSchema(S);
        }

        Delete.Table(DatabaseConfig.TableDepartments).InSchema(S);
    }
}
