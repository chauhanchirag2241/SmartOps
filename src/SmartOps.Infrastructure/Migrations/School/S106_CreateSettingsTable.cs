using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(106, "School template — settings and alerts")]
public sealed class S106_CreateSettingsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableSettings).Exists())
        {
            Create.Table(DatabaseConfig.TableSettings).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("key").AsString(100).NotNullable().Unique()
                .WithColumn("value").AsString(500).NotNullable()
                .WithColumn("description").AsString(500).Nullable()
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableAlerts).Exists())
        {
            Create.Table(DatabaseConfig.TableAlerts).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("title").AsString(200).NotNullable()
                .WithColumn("message").AsString(int.MaxValue).NotNullable()
                .WithColumn("alerttype").AsString(50).NotNullable().WithDefaultValue("info")
                .WithColumn("targetrole").AsString(100).Nullable()
                .WithColumn("targetuserid").AsGuid().Nullable()
                .WithColumn("isread").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("readon").AsDateTimeOffset().Nullable()
                .WithColumn("expiresat").AsDateTimeOffset().Nullable()
                .WithAuditColumns();

            Create.Index("ix_alerts_targetuserid")
                .OnTable(DatabaseConfig.TableAlerts).InSchema(S)
                .OnColumn("targetuserid").Ascending();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableAlerts).InSchema(S);
        Delete.Table(DatabaseConfig.TableSettings).InSchema(S);
    }
}
