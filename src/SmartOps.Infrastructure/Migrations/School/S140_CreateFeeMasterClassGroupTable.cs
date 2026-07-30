using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(140, "School template — fee master class groups")]
public sealed class S140_CreateFeeMasterClassGroupTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeeMasterClassGroup).Exists())
        {
            Create.Table(DatabaseConfig.TableFeeMasterClassGroup).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("feemasterid").AsGuid().NotNullable()
                .WithColumn("classgroupid").AsGuid().NotNullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableFeeMasterClassGroup}
    ADD CONSTRAINT fk_feemasterclassgroup_branchid FOREIGN KEY (branchid)
    REFERENCES {DatabaseConfig.Schema_Man}.{DatabaseConfig.TableSchoolBranches}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeeMasterClassGroup}
    ADD CONSTRAINT fk_feemasterclassgroup_feemasterid FOREIGN KEY (feemasterid)
    REFERENCES {S}.{DatabaseConfig.TableFeeMaster}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeeMasterClassGroup}
    ADD CONSTRAINT fk_feemasterclassgroup_classgroupid FOREIGN KEY (classgroupid)
    REFERENCES {S}.{DatabaseConfig.TableClassGroups}(id);

CREATE UNIQUE INDEX uq_feemasterclassgroup_master_group
    ON {S}.{DatabaseConfig.TableFeeMasterClassGroup} (feemasterid, classgroupid)
    WHERE isactive = true;

CREATE INDEX ix_feemasterclassgroup_feemasterid
    ON {S}.{DatabaseConfig.TableFeeMasterClassGroup} (feemasterid);
""");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableFeeMasterClassGroup).Exists())
        {
            Delete.Table(DatabaseConfig.TableFeeMasterClassGroup).InSchema(S);
        }
    }
}
