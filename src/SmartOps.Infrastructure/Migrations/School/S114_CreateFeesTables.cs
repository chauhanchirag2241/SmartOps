using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(114, "School template — fee structure (not academic-year scoped)")]
public sealed class S114_CreateFeesTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string StructureBranchUnique = "uq_feestructure_branch_version";
    private const string ClassFeeAmountUnique = "uq_classfeeamounts_classgroup_feehead_structure_year";
    private const string ClassFeeClassIndex = "ix_classfeeamounts_classgroupid";
    private const string ClassFeeVersionIndex = "ix_classfeeamounts_feestructureid";
    private const string FeeHeadVersionIndex = "ix_feehead_feestructureid";
    private const string FeePaymentStudentIndex = "ix_feepayments_studentid";
    private const string FeePaymentVersionIndex = "ix_feepayments_feestructureid";

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeeStructure).Exists())
        {
            Create.Table(DatabaseConfig.TableFeeStructure).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("versionnumber").AsInt32().NotNullable()
                .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("effectivedate").AsDate().Nullable()
                .WithColumn("publishedon").AsDateTime().Nullable()
                .WithColumn("activatedon").AsDateTime().Nullable()
                .WithAuditColumns();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableFeeStructure).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableFeeStructure).Constraint(StructureBranchUnique).Exists())
        {
            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableFeeStructure}
    ADD CONSTRAINT fk_feestructure_branchid FOREIGN KEY (branchid)
    REFERENCES {DatabaseConfig.Schema_Man}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE INDEX ix_feestructure_branchid ON {S}.{DatabaseConfig.TableFeeStructure} (branchid);
""");

            Create.UniqueConstraint(StructureBranchUnique)
                .OnTable(DatabaseConfig.TableFeeStructure).WithSchema(S)
                .Columns("branchid", "versionnumber");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeeHead).Exists())
        {
            Create.Table(DatabaseConfig.TableFeeHead).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("feestructureid").AsGuid().NotNullable()
                .WithColumn("name").AsString(200).NotNullable()
                .WithColumn("category").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("frequency").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("ismandatory").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("isrefundable").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Exists())
        {
            Create.Table(DatabaseConfig.TableClassFeeAmounts).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("feestructureid").AsGuid().NotNullable()
                .WithColumn("classgroupid").AsGuid().NotNullable()
                .WithColumn("feeheadid").AsGuid().NotNullable()
                .WithColumn("academicyearid").AsGuid().NotNullable()
                .WithColumn("amount").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeePayments).Exists())
        {
            Create.Table(DatabaseConfig.TableFeePayments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable()
                .WithColumn("feestructureid").AsGuid().NotNullable()
                .WithColumn("amount").AsDecimal(12, 2).NotNullable()
                .WithColumn("paymentmode").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("transactionno").AsString(100).Nullable()
                .WithColumn("paymentdate").AsDate().NotNullable()
                .WithColumn("remarks").AsString(500).Nullable()
                .WithColumn("receiptno").AsString(50).Nullable()
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeePaymentAllocations).Exists())
        {
            Create.Table(DatabaseConfig.TableFeePaymentAllocations).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("paymentid").AsGuid().NotNullable()
                .WithColumn("feeheadid").AsGuid().NotNullable()
                .WithColumn("amount").AsDecimal(12, 2).NotNullable()
                .WithAuditColumns();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Constraint(ClassFeeAmountUnique).Exists())
        {
            Create.UniqueConstraint(ClassFeeAmountUnique)
                .OnTable(DatabaseConfig.TableClassFeeAmounts).WithSchema(S)
                .Columns("classgroupid", "feeheadid", "feestructureid", "academicyearid");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Index(ClassFeeClassIndex).Exists())
        {
            Create.Index(ClassFeeClassIndex)
                .OnTable(DatabaseConfig.TableClassFeeAmounts).InSchema(S)
                .OnColumn("classgroupid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Index(ClassFeeVersionIndex).Exists())
        {
            Create.Index(ClassFeeVersionIndex)
                .OnTable(DatabaseConfig.TableClassFeeAmounts).InSchema(S)
                .OnColumn("feestructureid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeeHead).Index(FeeHeadVersionIndex).Exists())
        {
            Create.Index(FeeHeadVersionIndex)
                .OnTable(DatabaseConfig.TableFeeHead).InSchema(S)
                .OnColumn("feestructureid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeePayments).Index(FeePaymentStudentIndex).Exists())
        {
            Create.Index(FeePaymentStudentIndex)
                .OnTable(DatabaseConfig.TableFeePayments).InSchema(S)
                .OnColumn("studentid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeePayments).Index(FeePaymentVersionIndex).Exists())
        {
            Create.Index(FeePaymentVersionIndex)
                .OnTable(DatabaseConfig.TableFeePayments).InSchema(S)
                .OnColumn("feestructureid").Ascending();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentAcademics).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableStudentAcademics).Column("feestructureid").Exists())
        {
            Alter.Table(DatabaseConfig.TableStudentAcademics).InSchema(S)
                .AddColumn("feestructureid").AsGuid().Nullable();
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentAcademics).Column("feestructureid").Exists())
        {
            Delete.Column("feestructureid").FromTable(DatabaseConfig.TableStudentAcademics).InSchema(S);
        }

        Delete.Table(DatabaseConfig.TableFeePaymentAllocations).InSchema(S);
        Delete.Table(DatabaseConfig.TableFeePayments).InSchema(S);
        Delete.Table(DatabaseConfig.TableClassFeeAmounts).InSchema(S);
        Delete.Table(DatabaseConfig.TableFeeHead).InSchema(S);
        Delete.Table(DatabaseConfig.TableFeeStructure).InSchema(S);
    }
}
