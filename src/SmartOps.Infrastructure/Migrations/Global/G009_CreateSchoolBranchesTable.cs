using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Migration(9, "Global — school branches")]
public sealed class G009_CreateSchoolBranchesTable : Migration
{
    public override void Up()
    {
        if (Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableSchoolBranches).Exists())
        {
            return;
        }

        Create.Table(DatabaseConfig.TableSchoolBranches).InSchema(DatabaseConfig.Schema_Global)
            .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
            .WithColumn("schoolid").AsGuid().NotNullable()
            .WithColumn("name").AsString(200).NotNullable()
            .WithColumn("email").AsString(256).Nullable()
            .WithColumn("address").AsString(500).Nullable()
            .WithColumn("isheadoffice").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithAuditColumns();

        Create.ForeignKey("fk_school_branches_school")
            .FromTable(DatabaseConfig.TableSchoolBranches).InSchema(DatabaseConfig.Schema_Global)
            .ForeignColumn("schoolid")
            .ToTable(DatabaseConfig.TableSchools).InSchema(DatabaseConfig.Schema_Global)
            .PrimaryColumn("id")
            .OnDelete(System.Data.Rule.Cascade);
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableSchoolBranches).InSchema(DatabaseConfig.Schema_Global);
}
