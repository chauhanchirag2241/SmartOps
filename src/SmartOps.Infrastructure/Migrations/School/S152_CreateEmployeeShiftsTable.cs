using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(152, "School — employee ↔ shift master mapping")]
public sealed class S152_CreateEmployeeShiftsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableEmployeeShifts).Exists())
        {
            Create.Table(DatabaseConfig.TableEmployeeShifts).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("employeeid").AsGuid().NotNullable()
                    .ForeignKey("fk_employeeshifts_employeeid", S, DatabaseConfig.TableEmployees, "id")
                .WithColumn("shiftid").AsGuid().NotNullable()
                    .ForeignKey("fk_employeeshifts_shiftid", S, DatabaseConfig.TableShifts, "id")
                .WithAuditColumns();

            Create.Index("ix_employeeshifts_employeeid")
                .OnTable(DatabaseConfig.TableEmployeeShifts).InSchema(S)
                .OnColumn("employeeid").Ascending();

            Create.Index("ix_employeeshifts_shiftid")
                .OnTable(DatabaseConfig.TableEmployeeShifts).InSchema(S)
                .OnColumn("shiftid").Ascending();

            Execute.Sql($"""
CREATE UNIQUE INDEX uq_employeeshifts_employee_shift
ON {S}.{DatabaseConfig.TableEmployeeShifts} (employeeid, shiftid)
WHERE isactive = true;
""");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployeeShifts).Exists())
        {
            Delete.Table(DatabaseConfig.TableEmployeeShifts).InSchema(S);
        }
    }
}
