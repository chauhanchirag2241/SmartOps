using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(101, "School template — shifts, class groups, and class sections")]
public sealed class S101_CreateClassesTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Man;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableShifts).Exists())
        {
            Create.Table(DatabaseConfig.TableShifts).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("shiftname").AsString(100).NotNullable()
                .WithColumn("starttime").AsString(5).NotNullable()
                .WithColumn("endtime").AsString(5).NotNullable()
                .WithColumn("displayorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableShifts}
    ADD CONSTRAINT fk_shifts_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE UNIQUE INDEX uq_shifts_branch_name
    ON {S}.{DatabaseConfig.TableShifts} (branchid, lower(shiftname))
    WHERE isactive = true;

CREATE INDEX ix_shifts_branchid ON {S}.{DatabaseConfig.TableShifts} (branchid);
""");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassGroups).Exists())
        {
            Create.Table(DatabaseConfig.TableClassGroups).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("classname").AsString(50).NotNullable()
                .WithColumn("description").AsString(1000).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableClassGroups}
    ADD CONSTRAINT fk_classgroups_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE UNIQUE INDEX uq_classgroups_identity
    ON {S}.{DatabaseConfig.TableClassGroups} (branchid, lower(classname))
    WHERE isactive = true;

CREATE INDEX ix_classgroups_branchid
    ON {S}.{DatabaseConfig.TableClassGroups} (branchid);
""");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClasses).Exists())
        {
            Create.Table(DatabaseConfig.TableClasses).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classgroupid").AsGuid().NotNullable()
                    .ForeignKey("fk_classes_classgroupid", S, DatabaseConfig.TableClassGroups, "id")
                .WithColumn("section").AsString(100).NotNullable()
                .WithColumn("capacity").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("roomnumber").AsString(50).Nullable()
                .WithColumn("shiftid").AsGuid().Nullable()
                    .ForeignKey("fk_classes_shiftid", S, DatabaseConfig.TableShifts, "id")
                .WithAuditColumns();

            Execute.Sql($"""
CREATE UNIQUE INDEX uq_classes_identity
    ON {S}.{DatabaseConfig.TableClasses} (classgroupid, lower(section))
    WHERE isactive = true;

CREATE INDEX ix_classes_classgroupid
    ON {S}.{DatabaseConfig.TableClasses} (classgroupid);
""");
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableClasses).InSchema(S);
        Delete.Table(DatabaseConfig.TableClassGroups).InSchema(S);
        Delete.Table(DatabaseConfig.TableShifts).InSchema(S);
    }
}
