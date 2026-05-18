using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Migration(13, "Global — claims, roleclaims, userclaims")]
public sealed class G013_CreateClaimsTables : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableClaims).Exists())
        {
            Create.Table(DatabaseConfig.TableClaims).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("code").AsString(100).NotNullable().Unique()
                .WithColumn("name").AsString(200).NotNullable()
                .WithColumn("claimtype").AsString(50).NotNullable().WithDefaultValue("Scope")
                .WithColumn("description").AsCustom("text").Nullable()
                .WithAuditColumns();
        }

        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableRoleClaims).Exists())
        {
            Create.Table(DatabaseConfig.TableRoleClaims).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("roleid").AsGuid().NotNullable()
                    .ForeignKey("fk_roleclaims_roleid", DatabaseConfig.Schema_Global, DatabaseConfig.TableRoles, "id")
                .WithColumn("claimid").AsGuid().NotNullable()
                    .ForeignKey("fk_roleclaims_claimid", DatabaseConfig.Schema_Global, DatabaseConfig.TableClaims, "id")
                .WithColumn("value").AsString(500).Nullable()
                .WithAuditColumns();

            Create.PrimaryKey("pk_roleclaims")
                .OnTable(DatabaseConfig.TableRoleClaims).WithSchema(DatabaseConfig.Schema_Global)
                .Columns("roleid", "claimid");
        }

        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableUserClaims).Exists())
        {
            Create.Table(DatabaseConfig.TableUserClaims).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("userid").AsGuid().NotNullable()
                    .ForeignKey("fk_userclaims_userid", DatabaseConfig.Schema_Global, DatabaseConfig.TableUsers, "id")
                .WithColumn("claimid").AsGuid().NotNullable()
                    .ForeignKey("fk_userclaims_claimid", DatabaseConfig.Schema_Global, DatabaseConfig.TableClaims, "id")
                .WithColumn("schoolid").AsGuid().Nullable()
                    .ForeignKey("fk_userclaims_schoolid", DatabaseConfig.Schema_Global, DatabaseConfig.TableSchools, "id")
                .WithColumn("value").AsString(500).Nullable()
                .WithColumn("expiresat").AsDateTimeOffset().Nullable()
                .WithAuditColumns();

            Create.PrimaryKey("pk_userclaims")
                .OnTable(DatabaseConfig.TableUserClaims).WithSchema(DatabaseConfig.Schema_Global)
                .Columns("userid", "claimid", "schoolid");

            Create.Index("ix_userclaims_userid_schoolid")
                .OnTable(DatabaseConfig.TableUserClaims).InSchema(DatabaseConfig.Schema_Global)
                .OnColumn("userid").Ascending()
                .OnColumn("schoolid").Ascending();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableUserClaims).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableRoleClaims).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableClaims).InSchema(DatabaseConfig.Schema_Global);
    }
}
