using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(1, "Global — users")]
public sealed class G001_CreateUsersTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Exists())
        {
            Create.Schema(DatabaseConfig.Schema_Global);
        }

        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableUsers).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("username").AsString(100).NotNullable().Unique()
            .WithColumn("email").AsString(256).NotNullable().Unique()
            .WithColumn("passwordhash").AsCustom("text").NotNullable()
            .WithColumn("securitystamp").AsCustom("text").Nullable()
            .WithColumn("lockoutend").AsDateTimeOffset().Nullable()
            .WithColumn("accessfailedcount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("lockoutenabled").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global);
}
