using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

/// <summary>
/// Class-wise academic periods (used by academics). Fee period tables were removed with the old fees module.
/// </summary>
[Tags("School")]
[Migration(124, "School template — class-wise academic periods")]
public sealed class S124_FeeSemesterRestructure : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string ClassPeriodUnique = "uq_classacademicperiods_classgroup_index";

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassAcademicPeriods).Exists())
        {
            Create.Table(DatabaseConfig.TableClassAcademicPeriods).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classgroupid").AsGuid().NotNullable()
                .WithColumn("periodindex").AsInt32().NotNullable()
                .WithColumn("name").AsString(100).NotNullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableClassAcademicPeriods}
    ADD CONSTRAINT fk_classacademicperiods_classgroup FOREIGN KEY (classgroupid)
    REFERENCES {S}.{DatabaseConfig.TableClassGroups}(id),
    ADD CONSTRAINT ck_classacademicperiods_index CHECK (periodindex > 0);

CREATE UNIQUE INDEX {ClassPeriodUnique}
    ON {S}.{DatabaseConfig.TableClassAcademicPeriods} (classgroupid, periodindex)
    WHERE isactive = true;

CREATE UNIQUE INDEX uq_classacademicperiods_classgroup_name
    ON {S}.{DatabaseConfig.TableClassAcademicPeriods} (classgroupid, LOWER(name))
    WHERE isactive = true;
""");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableClassAcademicPeriods).Exists())
            Delete.Table(DatabaseConfig.TableClassAcademicPeriods).InSchema(S);
    }
}
