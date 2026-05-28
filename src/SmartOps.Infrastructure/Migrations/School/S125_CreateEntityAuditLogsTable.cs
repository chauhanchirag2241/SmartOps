using FluentMigrator;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(125, "School template — entity audit log table for field-level change history")]
public sealed class S125_CreateEntityAuditLogsTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableEntityAuditLogs).Exists())
        {
            return;
        }

        Execute.Sql($"""
            CREATE TABLE IF NOT EXISTS {S}.{DatabaseConfig.TableEntityAuditLogs} (
                id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
                entityname    VARCHAR(100) NOT NULL,
                entityid      UUID         NOT NULL,
                action        VARCHAR(20)  NOT NULL,
                changedby     UUID         NOT NULL,
                changedon     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
                changes       JSONB        NOT NULL DEFAULT '[]'
            );
            CREATE INDEX IF NOT EXISTS ix_audit_entity
                ON {S}.{DatabaseConfig.TableEntityAuditLogs} (entityname, entityid);
            CREATE INDEX IF NOT EXISTS ix_audit_changedon
                ON {S}.{DatabaseConfig.TableEntityAuditLogs} (changedon DESC);
            """);
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableEntityAuditLogs).Exists())
        {
            Execute.Sql($"DROP TABLE IF EXISTS {S}.{DatabaseConfig.TableEntityAuditLogs};");
        }
    }
}
