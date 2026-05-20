using FluentMigrator;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

/// <summary>Retired — claim tables were never used; access uses relational mappings and menu RBAC.</summary>
[Migration(13, "Global — claims (retired)")]
public sealed class G013_CreateClaimsTables : Migration
{
    public override void Up()
    {
        // Intentionally empty. Legacy claim schema removed in favor of ClassSubjectTeacherMappings.
    }

    public override void Down()
    {
        const string claims = "claims";
        const string roleClaims = "roleclaims";
        const string userClaims = "userclaims";

        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(userClaims).Exists())
        {
            Delete.Table(userClaims).InSchema(DatabaseConfig.Schema_Global);
        }

        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(roleClaims).Exists())
        {
            Delete.Table(roleClaims).InSchema(DatabaseConfig.Schema_Global);
        }

        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(claims).Exists())
        {
            Delete.Table(claims).InSchema(DatabaseConfig.Schema_Global);
        }
    }
}
