using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(148, "School template — calendar event class targeting")]
public sealed class S148_CreateCalendarEventClassesTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string EventClassUnique = "uq_calendareventclasses_event_class";

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableCalendarEventClasses).Exists())
        {
            Create.Table(DatabaseConfig.TableCalendarEventClasses).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("calendareventid").AsGuid().NotNullable()
                .WithColumn("classid").AsGuid().NotNullable()
                .WithAuditColumns();

            Create.UniqueConstraint(EventClassUnique)
                .OnTable(DatabaseConfig.TableCalendarEventClasses).WithSchema(S)
                .Columns("calendareventid", "classid");

            Create.Index("ix_calendareventclasses_eventid")
                .OnTable(DatabaseConfig.TableCalendarEventClasses).InSchema(S)
                .OnColumn("calendareventid").Ascending();
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableCalendarEventClasses).Exists())
        {
            Delete.Table(DatabaseConfig.TableCalendarEventClasses).InSchema(S);
        }
    }
}
