using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(142, "School template — fee payment + payment lines (collection snapshots)")]
public sealed class S142_CreateFeePaymentTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Man;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeePayment).Exists())
        {
            Create.Table(DatabaseConfig.TableFeePayment).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("studentid").AsGuid().NotNullable()
                .WithColumn("feemasterid").AsGuid().NotNullable()
                .WithColumn("academicperiodid").AsGuid().Nullable()
                .WithColumn("paymentdate").AsDateTime().NotNullable()
                .WithColumn("paymentmethod").AsString(30).NotNullable().WithDefaultValue("Cash")
                .WithColumn("totalamount").AsDecimal(18, 2).NotNullable()
                .WithColumn("remarks").AsString(500).Nullable()
                .WithColumn("collectedbyuserid").AsGuid().Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableFeePayment}
    ADD CONSTRAINT fk_feepayment_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeePayment}
    ADD CONSTRAINT fk_feepayment_studentid FOREIGN KEY (studentid)
    REFERENCES {S}.{DatabaseConfig.TableStudents}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeePayment}
    ADD CONSTRAINT fk_feepayment_feemasterid FOREIGN KEY (feemasterid)
    REFERENCES {S}.{DatabaseConfig.TableFeeMaster}(id);

CREATE INDEX ix_feepayment_studentid
    ON {S}.{DatabaseConfig.TableFeePayment} (studentid);

CREATE INDEX ix_feepayment_feemasterid
    ON {S}.{DatabaseConfig.TableFeePayment} (feemasterid);

CREATE INDEX ix_feepayment_student_master
    ON {S}.{DatabaseConfig.TableFeePayment} (studentid, feemasterid)
    WHERE isactive = true;
""");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeePaymentLine).Exists())
        {
            Create.Table(DatabaseConfig.TableFeePaymentLine).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("feepaymentid").AsGuid().NotNullable()
                .WithColumn("feeheadid").AsGuid().NotNullable()
                .WithColumn("feeheadname").AsString(200).NotNullable()
                .WithColumn("dueamount").AsDecimal(18, 2).NotNullable()
                .WithColumn("paidamount").AsDecimal(18, 2).NotNullable()
                .WithColumn("ismandatory").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("iseditable").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableFeePaymentLine}
    ADD CONSTRAINT fk_feepaymentline_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeePaymentLine}
    ADD CONSTRAINT fk_feepaymentline_paymentid FOREIGN KEY (feepaymentid)
    REFERENCES {S}.{DatabaseConfig.TableFeePayment}(id);

ALTER TABLE {S}.{DatabaseConfig.TableFeePaymentLine}
    ADD CONSTRAINT fk_feepaymentline_feeheadid FOREIGN KEY (feeheadid)
    REFERENCES {S}.{DatabaseConfig.TableFeeHead}(id);

CREATE INDEX ix_feepaymentline_paymentid
    ON {S}.{DatabaseConfig.TableFeePaymentLine} (feepaymentid);

CREATE INDEX ix_feepaymentline_feeheadid
    ON {S}.{DatabaseConfig.TableFeePaymentLine} (feeheadid);
""");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableFeePaymentLine).Exists())
        {
            Delete.Table(DatabaseConfig.TableFeePaymentLine).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableFeePayment).Exists())
        {
            Delete.Table(DatabaseConfig.TableFeePayment).InSchema(S);
        }
    }
}
