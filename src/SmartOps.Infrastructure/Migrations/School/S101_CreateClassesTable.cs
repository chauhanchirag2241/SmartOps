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
                .WithColumn("streamgroup").AsInt32().Nullable()
                .WithColumn("medium").AsInt32().Nullable()
                .WithColumn("description").AsString(1000).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableClassGroups}
    ADD CONSTRAINT fk_classgroups_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);
""");

            Create.UniqueConstraint("uq_classgroups_identity")
                .OnTable(DatabaseConfig.TableClassGroups).WithSchema(S)
                .Columns("branchid", "classname", "streamgroup");

            Create.Index("ix_classgroups_branchid")
                .OnTable(DatabaseConfig.TableClassGroups).InSchema(S)
                .OnColumn("branchid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClasses).Exists())
        {
            Create.Table(DatabaseConfig.TableClasses).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classgroupid").AsGuid().NotNullable()
                    .ForeignKey("fk_classes_classgroupid", S, DatabaseConfig.TableClassGroups, "id")
                .WithColumn("section").AsInt32().NotNullable().WithDefaultValue(1)
                .WithColumn("capacity").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("roomnumber").AsString(50).Nullable()
                .WithColumn("shiftid").AsGuid().Nullable()
                    .ForeignKey("fk_classes_shiftid", S, DatabaseConfig.TableShifts, "id")
                .WithAuditColumns();

            Create.UniqueConstraint("uq_classes_identity")
                .OnTable(DatabaseConfig.TableClasses).WithSchema(S)
                .Columns("classgroupid", "section");

            Create.Index("ix_classes_classgroupid")
                .OnTable(DatabaseConfig.TableClasses).InSchema(S)
                .OnColumn("classgroupid").Ascending();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableClasses).InSchema(S);
        Delete.Table(DatabaseConfig.TableClassGroups).InSchema(S);
        Delete.Table(DatabaseConfig.TableShifts).InSchema(S);
    }
}
