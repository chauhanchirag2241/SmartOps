using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Migration(8, "Global — refresh tokens")]
public sealed class G008_CreateRefreshTokensTable : Migration
{
    public override void Up()
    {
        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableRefreshTokens).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableRefreshTokens).InSchema(DatabaseConfig.Schema_Global)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("userid").AsGuid().NotNullable()
            .WithColumn("token").AsCustom("text").NotNullable().Unique()
            .WithColumn("expiresat").AsDateTimeOffset().NotNullable()
            .WithColumn("isrevoked").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Execute.Sql($"""
ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRefreshTokens}
    ADD CONSTRAINT fk_refresh_tokens_user FOREIGN KEY (userid) REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;
""");
    }

    public override void Down()
    {
        Execute.Sql($"ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRefreshTokens} DROP CONSTRAINT IF EXISTS fk_refresh_tokens_user;");
        Delete.Table(DatabaseConfig.TableRefreshTokens).InSchema(DatabaseConfig.Schema_Global);
    }
}
