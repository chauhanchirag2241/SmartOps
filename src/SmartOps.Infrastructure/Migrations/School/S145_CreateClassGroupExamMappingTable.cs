using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(145, "School template — exam group class group mappings")]
public sealed class S145_CreateClassGroupExamMappingTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassGroupExamMappings).Exists())
        {
            Create.Table(DatabaseConfig.TableClassGroupExamMappings).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("examgroupid").AsGuid().NotNullable()
                .WithColumn("classgroupid").AsGuid().NotNullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableClassGroupExamMappings}
    ADD CONSTRAINT fk_classgroupexammappings_branchid FOREIGN KEY (branchid)
    REFERENCES {DatabaseConfig.Schema_Man}.{DatabaseConfig.TableSchoolBranches}(id);

ALTER TABLE {S}.{DatabaseConfig.TableClassGroupExamMappings}
    ADD CONSTRAINT fk_classgroupexammappings_examgroupid FOREIGN KEY (examgroupid)
    REFERENCES {S}.{DatabaseConfig.TableExamGroups}(id);

ALTER TABLE {S}.{DatabaseConfig.TableClassGroupExamMappings}
    ADD CONSTRAINT fk_classgroupexammappings_classgroupid FOREIGN KEY (classgroupid)
    REFERENCES {S}.{DatabaseConfig.TableClassGroups}(id);

CREATE UNIQUE INDEX uq_classgroupexammappings_group_class
    ON {S}.{DatabaseConfig.TableClassGroupExamMappings} (examgroupid, classgroupid)
    WHERE isactive = true;

CREATE INDEX ix_classgroupexammappings_examgroupid
    ON {S}.{DatabaseConfig.TableClassGroupExamMappings} (examgroupid);

CREATE INDEX ix_classgroupexammappings_classgroupid
    ON {S}.{DatabaseConfig.TableClassGroupExamMappings} (classgroupid);
""");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableClassGroupExamMappings).Exists())
        {
            Delete.Table(DatabaseConfig.TableClassGroupExamMappings).InSchema(S);
        }
    }
}
