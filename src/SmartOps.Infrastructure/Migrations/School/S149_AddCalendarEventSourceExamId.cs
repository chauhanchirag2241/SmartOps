using FluentMigrator;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(149, "School template — link calendar events to exams")]
public sealed class S149_AddCalendarEventSourceExamId : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableCalendarEvents).Exists())
        {
            return;
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableCalendarEvents).Column("sourceexamid").Exists())
        {
            Alter.Table(DatabaseConfig.TableCalendarEvents).InSchema(S)
                .AddColumn("sourceexamid").AsGuid().Nullable();
        }

        Execute.Sql($"""
            CREATE UNIQUE INDEX IF NOT EXISTS uq_calendarevents_sourceexamid
            ON {S}.{DatabaseConfig.TableCalendarEvents} (sourceexamid)
            WHERE sourceexamid IS NOT NULL AND isactive = true;
            """);
    }

    public override void Down()
    {
        Execute.Sql($"""
            DROP INDEX IF EXISTS {S}.uq_calendarevents_sourceexamid;
            """);

        if (Schema.Schema(S).Table(DatabaseConfig.TableCalendarEvents).Column("sourceexamid").Exists())
        {
            Delete.Column("sourceexamid").FromTable(DatabaseConfig.TableCalendarEvents).InSchema(S);
        }
    }
}
