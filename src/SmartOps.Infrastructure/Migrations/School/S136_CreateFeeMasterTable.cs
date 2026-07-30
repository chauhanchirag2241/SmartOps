using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(136, "School template — fee master")]
public sealed class S136_CreateFeeMasterTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Man;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeeMaster).Exists())
        {
            Create.Table(DatabaseConfig.TableFeeMaster).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("feename").AsString(150).NotNullable()
                .WithColumn("feetype").AsString(30).NotNullable()
                .WithColumn("publishedon").AsDate().Nullable()
                .WithColumn("defaultduedate").AsDate().Nullable()
                .WithColumn("applicableto").AsString(30).NotNullable()
                .WithColumn("description").AsString(1000).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableFeeMaster}
    ADD CONSTRAINT fk_feemaster_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE UNIQUE INDEX uq_feemaster_branch_name
    ON {S}.{DatabaseConfig.TableFeeMaster} (branchid, lower(feename))
    WHERE isactive = true;

CREATE INDEX ix_feemaster_branchid ON {S}.{DatabaseConfig.TableFeeMaster} (branchid);
""");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableFeeMaster).Exists())
        {
            Delete.Table(DatabaseConfig.TableFeeMaster).InSchema(S);
        }
    }
}
