using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(9, "Global — school branches and user branch mappings")]
public sealed class G009_CreateSchoolBranchesTable : Migration
{
    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableSchoolBranches).Exists())
        {
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

        if (!Schema.Schema(DatabaseConfig.Schema_Global).Table(DatabaseConfig.TableUserBranchMappings).Exists())
        {
            Create.Table(DatabaseConfig.TableUserBranchMappings).InSchema(DatabaseConfig.Schema_Global)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("userid").AsGuid().NotNullable()
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("schoolid").AsGuid().NotNullable()
                .WithColumn("isdefault").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithAuditColumns();

            Create.ForeignKey("fk_userbranchmappings_user")
                .FromTable(DatabaseConfig.TableUserBranchMappings).InSchema(DatabaseConfig.Schema_Global)
                .ForeignColumn("userid")
                .ToTable(DatabaseConfig.TableUsers).InSchema(DatabaseConfig.Schema_Global)
                .PrimaryColumn("id")
                .OnDelete(System.Data.Rule.Cascade);

            Create.ForeignKey("fk_userbranchmappings_branch")
                .FromTable(DatabaseConfig.TableUserBranchMappings).InSchema(DatabaseConfig.Schema_Global)
                .ForeignColumn("branchid")
                .ToTable(DatabaseConfig.TableSchoolBranches).InSchema(DatabaseConfig.Schema_Global)
                .PrimaryColumn("id")
                .OnDelete(System.Data.Rule.Cascade);

            Create.ForeignKey("fk_userbranchmappings_school")
                .FromTable(DatabaseConfig.TableUserBranchMappings).InSchema(DatabaseConfig.Schema_Global)
                .ForeignColumn("schoolid")
                .ToTable(DatabaseConfig.TableSchools).InSchema(DatabaseConfig.Schema_Global)
                .PrimaryColumn("id")
                .OnDelete(System.Data.Rule.Cascade);

            Create.UniqueConstraint("uq_userbranchmappings_user_branch")
                .OnTable(DatabaseConfig.TableUserBranchMappings).WithSchema(DatabaseConfig.Schema_Global)
                .Columns("userid", "branchid");

            Create.Index("ix_userbranchmappings_userid")
                .OnTable(DatabaseConfig.TableUserBranchMappings).InSchema(DatabaseConfig.Schema_Global)
                .OnColumn("userid").Ascending();

            Create.Index("ix_userbranchmappings_schoolid")
                .OnTable(DatabaseConfig.TableUserBranchMappings).InSchema(DatabaseConfig.Schema_Global)
                .OnColumn("schoolid").Ascending();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableUserBranchMappings).InSchema(DatabaseConfig.Schema_Global);
        Delete.Table(DatabaseConfig.TableSchoolBranches).InSchema(DatabaseConfig.Schema_Global);
    }
}
