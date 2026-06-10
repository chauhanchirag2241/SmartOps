using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(14, "Global — user scope versions and authorization audit")]
public sealed class G014_CreateUserScopeVersionAndAuditTables : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableUserScopeVersions).Exists())
        {
            Create.Table(DatabaseConfig.TableUserScopeVersions).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("userid").AsGuid().PrimaryKey().NotNullable()
                    .ForeignKey("fk_userscopeversions_userid", DatabaseConfig.Schema_Global, DatabaseConfig.TableUsers, "id")
                .WithColumn("schoolid").AsGuid().NotNullable()
                    .ForeignKey("fk_userscopeversions_schoolid", DatabaseConfig.Schema_Global, DatabaseConfig.TableSchools, "id")
                .WithColumn("version").AsInt32().NotNullable().WithDefaultValue(1)
                .WithColumn("updatedon").AsDateTimeOffset().NotNullable()
                    .WithDefaultValue(RawSql.Insert("NOW()"));

            Create.Index("ix_userscopeversions_schoolid")
                .OnTable(DatabaseConfig.TableUserScopeVersions).InSchema(DatabaseConfig.Schema_Global)
                .OnColumn("schoolid").Ascending();
        }

        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableAuthorizationAuditLog).Exists())
        {
            Create.Table(DatabaseConfig.TableAuthorizationAuditLog).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("userid").AsGuid().Nullable()
                .WithColumn("action").AsString(100).NotNullable()
                .WithColumn("resource").AsString(200).Nullable()
                .WithColumn("decision").AsString(20).NotNullable()
                .WithColumn("metadata").AsCustom("jsonb").Nullable()
                .WithColumn("createdon").AsDateTimeOffset().NotNullable()
                    .WithDefaultValue(RawSql.Insert("NOW()"));

            Create.Index("ix_authorizationauditlog_userid_createdon")
                .OnTable(DatabaseConfig.TableAuthorizationAuditLog).InSchema(DatabaseConfig.Schema_Global)
                .OnColumn("userid").Ascending()
                .OnColumn("createdon").Descending();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableAuthorizationAuditLog).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableUserScopeVersions).InSchema(DatabaseConfig.Schema_Global);
    }
}
