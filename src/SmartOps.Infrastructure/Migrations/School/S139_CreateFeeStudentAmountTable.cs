using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(139, "School template — fee student amount overrides")]
public sealed class S139_CreateFeeStudentAmountTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Man;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeeStudentAmount).Exists())
        {
            Create.Table(DatabaseConfig.TableFeeStudentAmount).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("feemasterid").AsGuid().NotNullable()
                .WithColumn("feeheadid").AsGuid().NotNullable()
                .WithColumn("studentid").AsGuid().NotNullable()
                .WithColumn("academicperiodid").AsGuid().Nullable()
                .WithColumn("amount").AsDecimal(18, 2).Nullable()
                .WithColumn("isexcluded").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableFeeStudentAmount}
    ADD CONSTRAINT fk_feestudentamount_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeeStudentAmount}
    ADD CONSTRAINT fk_feestudentamount_feemasterid FOREIGN KEY (feemasterid)
    REFERENCES {S}.{DatabaseConfig.TableFeeMaster}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeeStudentAmount}
    ADD CONSTRAINT fk_feestudentamount_feeheadid FOREIGN KEY (feeheadid)
    REFERENCES {S}.{DatabaseConfig.TableFeeHead}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeeStudentAmount}
    ADD CONSTRAINT fk_feestudentamount_studentid FOREIGN KEY (studentid)
    REFERENCES {S}.{DatabaseConfig.TableStudents}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeeStudentAmount}
    ADD CONSTRAINT fk_feestudentamount_academicperiodid FOREIGN KEY (academicperiodid)
    REFERENCES {S}.{DatabaseConfig.TableClassAcademicPeriods}(id);

CREATE UNIQUE INDEX uq_feestudentamount_head_student_flat
    ON {S}.{DatabaseConfig.TableFeeStudentAmount} (feeheadid, studentid)
    WHERE isactive = true AND academicperiodid IS NULL;

CREATE UNIQUE INDEX uq_feestudentamount_head_student_period
    ON {S}.{DatabaseConfig.TableFeeStudentAmount} (feeheadid, studentid, academicperiodid)
    WHERE isactive = true AND academicperiodid IS NOT NULL;

CREATE INDEX ix_feestudentamount_feemasterid
    ON {S}.{DatabaseConfig.TableFeeStudentAmount} (feemasterid);

CREATE INDEX ix_feestudentamount_studentid
    ON {S}.{DatabaseConfig.TableFeeStudentAmount} (studentid);
""");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableFeeStudentAmount).Exists())
        {
            Delete.Table(DatabaseConfig.TableFeeStudentAmount).InSchema(S);
        }
    }
}
