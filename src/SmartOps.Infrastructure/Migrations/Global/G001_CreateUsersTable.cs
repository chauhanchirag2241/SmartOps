using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(1, "Global — usertypes + users")]
public sealed class G001_CreateUsersTable : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Exists())
        {
            Create.Schema(DatabaseConfig.Schema_Global);
        }

        string g = DatabaseConfig.Schema_Global;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        if (!Schema.Schema(g).Table(DatabaseConfig.TableUserTypes).Exists())
        {
            Create.Table(DatabaseConfig.TableUserTypes).InSchema(g)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
                .WithColumn("name").AsString(100).NotNullable().Unique()
                .WithAuditColumns();

            foreach ((Guid id, string name) in UserTypeCodes.All)
            {
                Execute.Sql($"""
INSERT INTO {g}.{DatabaseConfig.TableUserTypes}
    (id, name, isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES
    ('{id}', '{name.Replace("'", "''")}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}');
""");
            }
        }

        if (Schema.Schema(g).Table(DatabaseConfig.TableUsers).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUsers).InSchema(g)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("firstname").AsString(50).NotNullable()
            .WithColumn("lastname").AsString(50).NotNullable()
            .WithColumn("mobile").AsString(20).Nullable()
            .WithColumn("usertypeid").AsGuid().NotNullable()
            .WithColumn("username").AsString(100).NotNullable().Unique()
            .WithColumn("email").AsString(256).NotNullable().Unique()
            .WithColumn("passwordhash").AsCustom("text").NotNullable()
            .WithColumn("securitystamp").AsCustom("text").Nullable()
            .WithColumn("lockoutend").AsDateTimeOffset().Nullable()
            .WithColumn("accessfailedcount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("lockoutenabled").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Execute.Sql($"""
ALTER TABLE {g}.{DatabaseConfig.TableUsers}
    ADD CONSTRAINT fk_users_usertype
    FOREIGN KEY (usertypeid) REFERENCES {g}.{DatabaseConfig.TableUserTypes}(id);
""");
    }

    public override void Down()
    {
        string g = DatabaseConfig.Schema_Global;
        Execute.Sql($"ALTER TABLE {g}.{DatabaseConfig.TableUsers} DROP CONSTRAINT IF EXISTS fk_users_usertype;");
        Delete.Table(DatabaseConfig.TableUsers).InSchema(g);
        Delete.Table(DatabaseConfig.TableUserTypes).InSchema(g);
    }
}
