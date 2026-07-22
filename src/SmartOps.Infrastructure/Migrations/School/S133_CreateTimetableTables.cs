using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(133, "School template — period templates, periods, timetable versions and slots")]
public sealed class S133_CreateTimetableTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Global;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TablePeriodTemplates).Exists())
        {
            Create.Table(DatabaseConfig.TablePeriodTemplates).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("name").AsString(100).NotNullable()
                .WithColumn("description").AsString(500).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TablePeriodTemplates}
    ADD CONSTRAINT fk_period_templates_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE UNIQUE INDEX uq_period_templates_branch_name
    ON {S}.{DatabaseConfig.TablePeriodTemplates} (branchid, lower(name))
    WHERE isactive = true;

CREATE INDEX ix_period_templates_branchid ON {S}.{DatabaseConfig.TablePeriodTemplates} (branchid);
""");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TablePeriods).Exists())
        {
            Create.Table(DatabaseConfig.TablePeriods).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("templateid").AsGuid().NotNullable()
                .WithColumn("name").AsString(100).NotNullable()
                .WithColumn("shortname").AsString(20).NotNullable()
                .WithColumn("periodorder").AsInt32().NotNullable()
                .WithColumn("starttime").AsString(10).NotNullable()
                .WithColumn("endtime").AsString(10).NotNullable()
                .WithColumn("isbreak").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithAuditColumns();

            Execute.Sql($"""
CREATE UNIQUE INDEX uq_periods_template_order
    ON {S}.{DatabaseConfig.TablePeriods} (templateid, periodorder)
    WHERE isactive = true;

CREATE INDEX ix_periods_templateid ON {S}.{DatabaseConfig.TablePeriods} (templateid);
""");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassTimetables).Exists())
        {
            Create.Table(DatabaseConfig.TableClassTimetables).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("academicyearid").AsGuid().NotNullable()
                .WithColumn("classid").AsGuid().NotNullable()
                .WithColumn("periodtemplateid").AsGuid().NotNullable()
                .WithColumn("effectivefrom").AsDate().NotNullable()
                .WithColumn("notes").AsString(500).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
CREATE UNIQUE INDEX uq_class_timetables_class_ay_from
    ON {S}.{DatabaseConfig.TableClassTimetables} (classid, academicyearid, effectivefrom)
    WHERE isactive = true;

CREATE INDEX ix_class_timetables_class_ay
    ON {S}.{DatabaseConfig.TableClassTimetables} (classid, academicyearid)
    WHERE isactive = true;

CREATE INDEX ix_class_timetables_template
    ON {S}.{DatabaseConfig.TableClassTimetables} (periodtemplateid)
    WHERE isactive = true;
""");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassTimetableSlots).Exists())
        {
            Create.Table(DatabaseConfig.TableClassTimetableSlots).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("timetableid").AsGuid().NotNullable()
                .WithColumn("dayofweek").AsInt32().NotNullable()
                .WithColumn("periodid").AsGuid().NotNullable()
                .WithColumn("subjectid").AsGuid().Nullable()
                .WithColumn("employeeid").AsGuid().Nullable()
                .WithColumn("roomno").AsString(50).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
CREATE UNIQUE INDEX uq_class_timetable_slots_cell
    ON {S}.{DatabaseConfig.TableClassTimetableSlots} (timetableid, dayofweek, periodid)
    WHERE isactive = true;

CREATE INDEX ix_class_timetable_slots_timetable
    ON {S}.{DatabaseConfig.TableClassTimetableSlots} (timetableid)
    WHERE isactive = true;

CREATE INDEX ix_class_timetable_slots_employee
    ON {S}.{DatabaseConfig.TableClassTimetableSlots} (employeeid, dayofweek, periodid)
    WHERE isactive = true AND employeeid IS NOT NULL;
""");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableClassTimetableSlots).Exists())
            Delete.Table(DatabaseConfig.TableClassTimetableSlots).InSchema(S);
        if (Schema.Schema(S).Table(DatabaseConfig.TableClassTimetables).Exists())
            Delete.Table(DatabaseConfig.TableClassTimetables).InSchema(S);
        if (Schema.Schema(S).Table(DatabaseConfig.TablePeriods).Exists())
            Delete.Table(DatabaseConfig.TablePeriods).InSchema(S);
        if (Schema.Schema(S).Table(DatabaseConfig.TablePeriodTemplates).Exists())
            Delete.Table(DatabaseConfig.TablePeriodTemplates).InSchema(S);
    }
}
