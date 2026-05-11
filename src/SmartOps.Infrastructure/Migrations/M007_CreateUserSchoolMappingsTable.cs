using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(007)]
public sealed class M007_CreateUserSchoolMappingsTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Exists())
        {
            Create.Schema(DatabaseConfig.Schema_Global);
        }

        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableUserSchoolMappings).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableUserSchoolMappings).InSchema(DatabaseConfig.Schema_Global)
            .WithColumn("userid").AsGuid().NotNullable()
            .WithColumn("schoolid").AsGuid().NotNullable()
            .WithColumn("role").AsString(100).NotNullable()
            .WithAuditColumns();

        Create.PrimaryKey("pk_user_school_mappings")
            .OnTable(DatabaseConfig.TableUserSchoolMappings)
            .WithSchema(DatabaseConfig.Schema_Global)
            .Columns("userid", "schoolid");

        Execute.Sql($"""
ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserSchoolMappings}
    ADD CONSTRAINT fk_user_school_mappings_user FOREIGN KEY (userid) REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUsers}(id) ON DELETE CASCADE;
""");

        Execute.Sql($"""
ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserSchoolMappings}
    ADD CONSTRAINT fk_user_school_mappings_school FOREIGN KEY (schoolid) REFERENCES {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools}(id) ON DELETE CASCADE;
""");
    }

    public override void Down()
    {
        Execute.Sql($"ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserSchoolMappings} DROP CONSTRAINT IF EXISTS fk_user_school_mappings_user;");
        Execute.Sql($"ALTER TABLE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserSchoolMappings} DROP CONSTRAINT IF EXISTS fk_user_school_mappings_school;");

        Delete.PrimaryKey("pk_user_school_mappings").FromTable(DatabaseConfig.TableUserSchoolMappings).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableUserSchoolMappings).InSchema(DatabaseConfig.Schema_Global);
    }
}
