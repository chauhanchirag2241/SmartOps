using FluentMigrator;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(126, "School template — academic year iscurrent flag")]
public sealed class S126_AddAcademicYearIsCurrent : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string IndexName = "uq_academicyears_single_current";

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableAcademicYears).Exists())
        {
            return;
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableAcademicYears).Column("iscurrent").Exists())
        {
            Alter.Table(DatabaseConfig.TableAcademicYears).InSchema(S)
                .AddColumn("iscurrent").AsBoolean().NotNullable().WithDefaultValue(false);
        }

        Execute.Sql($"""
            UPDATE {S}.{DatabaseConfig.TableAcademicYears} SET iscurrent = false;
            UPDATE {S}.{DatabaseConfig.TableAcademicYears} ay
            SET iscurrent = true
            WHERE ay.id = (
                SELECT id FROM {S}.{DatabaseConfig.TableAcademicYears}
                WHERE isactive = true
                ORDER BY startdate DESC NULLS LAST, createdon DESC
                LIMIT 1
            );
            """);

        Execute.Sql($"""
            CREATE UNIQUE INDEX IF NOT EXISTS {IndexName}
                ON {S}.{DatabaseConfig.TableAcademicYears} (iscurrent)
                WHERE iscurrent = true AND isactive = true;
            """);
    }

    public override void Down()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableAcademicYears).Exists())
        {
            return;
        }

        Execute.Sql($"DROP INDEX IF EXISTS {S}.{IndexName};");

        if (Schema.Schema(S).Table(DatabaseConfig.TableAcademicYears).Column("iscurrent").Exists())
        {
            Delete.Column("iscurrent").FromTable(DatabaseConfig.TableAcademicYears).InSchema(S);
        }
    }
}
