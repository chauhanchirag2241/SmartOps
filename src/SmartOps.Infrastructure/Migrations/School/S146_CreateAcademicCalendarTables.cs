using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(146, "School template — academic calendar tables")]
public sealed class S146_CreateAcademicCalendarTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private static readonly Guid HolidayTypeId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid EventTypeId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly Guid ExamTypeId = Guid.Parse("20000000-0000-0000-0000-000000000003");

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableCalendarEventTypes).Exists())
        {
            Create.Table(DatabaseConfig.TableCalendarEventTypes).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("name").AsString(100).NotNullable()
                .WithColumn("code").AsString(50).NotNullable()
                .WithColumn("color").AsString(20).NotNullable().WithDefaultValue("#5B8DEF")
                .WithColumn("isnonworkingdefault").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("displayorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Create.Index("ix_calendareventtypes_code")
                .OnTable(DatabaseConfig.TableCalendarEventTypes).InSchema(S)
                .OnColumn("code").Ascending()
                .WithOptions().Unique();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableCalendarWeekendSettings).Exists())
        {
            Create.Table(DatabaseConfig.TableCalendarWeekendSettings).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("sundayoff").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("saturdayoff").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("mondayoff").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("tuesdayoff").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("wednesdayoff").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("thursdayoff").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("fridayoff").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithAuditColumns();

            Create.Index("ix_calendarweekendsettings_branchid")
                .OnTable(DatabaseConfig.TableCalendarWeekendSettings).InSchema(S)
                .OnColumn("branchid").Ascending()
                .WithOptions().Unique();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableCalendarEvents).Exists())
        {
            Create.Table(DatabaseConfig.TableCalendarEvents).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("academicyearid").AsGuid().NotNullable()
                .WithColumn("eventtypeid").AsGuid().NotNullable()
                .WithColumn("title").AsString(200).NotNullable()
                .WithColumn("description").AsString(int.MaxValue).Nullable()
                .WithColumn("startdate").AsDate().NotNullable()
                .WithColumn("enddate").AsDate().NotNullable()
                .WithColumn("appliestostudents").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("appliestoteachers").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("appliestostaff").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("isnonworkingday").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("color").AsString(20).Nullable()
                .WithAuditColumns();

            Create.Index("ix_calendarevents_branch_dates")
                .OnTable(DatabaseConfig.TableCalendarEvents).InSchema(S)
                .OnColumn("branchid").Ascending()
                .OnColumn("startdate").Ascending()
                .OnColumn("enddate").Ascending();

            Create.Index("ix_calendarevents_academicyearid")
                .OnTable(DatabaseConfig.TableCalendarEvents).InSchema(S)
                .OnColumn("academicyearid").Ascending();

            Create.Index("ix_calendarevents_eventtypeid")
                .OnTable(DatabaseConfig.TableCalendarEvents).InSchema(S)
                .OnColumn("eventtypeid").Ascending();
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Execute.Sql($"""
INSERT INTO {S}.{DatabaseConfig.TableCalendarEventTypes}
    (id, name, code, color, isnonworkingdefault, displayorder, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{HolidayTypeId}', 'Holiday', 'HOLIDAY', '#E57373', true, 1, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (SELECT 1 FROM {S}.{DatabaseConfig.TableCalendarEventTypes} WHERE code = 'HOLIDAY');

INSERT INTO {S}.{DatabaseConfig.TableCalendarEventTypes}
    (id, name, code, color, isnonworkingdefault, displayorder, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{EventTypeId}', 'Event', 'EVENT', '#64B5F6', false, 2, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (SELECT 1 FROM {S}.{DatabaseConfig.TableCalendarEventTypes} WHERE code = 'EVENT');

INSERT INTO {S}.{DatabaseConfig.TableCalendarEventTypes}
    (id, name, code, color, isnonworkingdefault, displayorder, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{ExamTypeId}', 'Exam', 'EXAM', '#FFB74D', false, 3, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (SELECT 1 FROM {S}.{DatabaseConfig.TableCalendarEventTypes} WHERE code = 'EXAM');
""");
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableCalendarEvents).Exists())
        {
            Delete.Table(DatabaseConfig.TableCalendarEvents).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableCalendarWeekendSettings).Exists())
        {
            Delete.Table(DatabaseConfig.TableCalendarWeekendSettings).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableCalendarEventTypes).Exists())
        {
            Delete.Table(DatabaseConfig.TableCalendarEventTypes).InSchema(S);
        }
    }
}
