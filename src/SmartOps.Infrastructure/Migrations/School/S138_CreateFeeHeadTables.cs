using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(138, "School template — fee head and period amounts")]
public sealed class S138_CreateFeeHeadTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Man;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeeHead).Exists())
        {
            Create.Table(DatabaseConfig.TableFeeHead).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("feemasterid").AsGuid().NotNullable()
                .WithColumn("feeheadname").AsString(150).NotNullable()
                .WithColumn("ismandatory").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("iseditable").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("amount").AsDecimal(18, 2).Nullable()
                .WithColumn("applicablemonths").AsString(100).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableFeeHead}
    ADD CONSTRAINT fk_feehead_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeeHead}
    ADD CONSTRAINT fk_feehead_feemasterid FOREIGN KEY (feemasterid)
    REFERENCES {S}.{DatabaseConfig.TableFeeMaster}(id);

CREATE UNIQUE INDEX uq_feehead_master_name
    ON {S}.{DatabaseConfig.TableFeeHead} (feemasterid, lower(feeheadname))
    WHERE isactive = true;

CREATE INDEX ix_feehead_feemasterid ON {S}.{DatabaseConfig.TableFeeHead} (feemasterid);
CREATE INDEX ix_feehead_branchid ON {S}.{DatabaseConfig.TableFeeHead} (branchid);
""");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeeHeadPeriodAmount).Exists())
        {
            Create.Table(DatabaseConfig.TableFeeHeadPeriodAmount).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("feeheadid").AsGuid().NotNullable()
                .WithColumn("classgroupid").AsGuid().NotNullable()
                .WithColumn("academicperiodid").AsGuid().NotNullable()
                .WithColumn("amount").AsDecimal(18, 2).NotNullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableFeeHeadPeriodAmount}
    ADD CONSTRAINT fk_feeheadperiodamount_feeheadid FOREIGN KEY (feeheadid)
    REFERENCES {S}.{DatabaseConfig.TableFeeHead}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeeHeadPeriodAmount}
    ADD CONSTRAINT fk_feeheadperiodamount_classgroupid FOREIGN KEY (classgroupid)
    REFERENCES {S}.{DatabaseConfig.TableClassGroups}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeeHeadPeriodAmount}
    ADD CONSTRAINT fk_feeheadperiodamount_academicperiodid FOREIGN KEY (academicperiodid)
    REFERENCES {S}.{DatabaseConfig.TableClassAcademicPeriods}(id);

CREATE UNIQUE INDEX uq_feeheadperiodamount_head_period
    ON {S}.{DatabaseConfig.TableFeeHeadPeriodAmount} (feeheadid, academicperiodid)
    WHERE isactive = true;

CREATE INDEX ix_feeheadperiodamount_feeheadid
    ON {S}.{DatabaseConfig.TableFeeHeadPeriodAmount} (feeheadid);
""");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableFeeHeadPeriodAmount).Exists())
        {
            Delete.Table(DatabaseConfig.TableFeeHeadPeriodAmount).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableFeeHead).Exists())
        {
            Delete.Table(DatabaseConfig.TableFeeHead).InSchema(S);
        }
    }
}
