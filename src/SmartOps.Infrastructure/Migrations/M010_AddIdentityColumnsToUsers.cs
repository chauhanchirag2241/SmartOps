using FluentMigrator;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(010)]
public sealed class M010_AddIdentityColumnsToUsers : Migration
{
    public override void Up()
    {
        Alter.Table(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global)
            .AddColumn("securitystamp").AsCustom("text").Nullable();

        Alter.Table(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global)
            .AddColumn("lockoutend").AsDateTimeOffset().Nullable();

        Alter.Table(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global)
            .AddColumn("accessfailedcount").AsInt32().NotNullable().WithDefaultValue(0);

        Alter.Table(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global)
            .AddColumn("lockoutenabled").AsBoolean().NotNullable().WithDefaultValue(false);
    }

    public override void Down()
    {
        Delete.Column("securitystamp").FromTable(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global);
        Delete.Column("lockoutend").FromTable(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global);
        Delete.Column("accessfailedcount").FromTable(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global);
        Delete.Column("lockoutenabled").FromTable(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global);
    }
}
