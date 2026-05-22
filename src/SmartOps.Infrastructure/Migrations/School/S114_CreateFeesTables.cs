using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Migration(114, "School template — fees")]
public sealed class S114_CreateFeesTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string ClassFeeAmountUnique = "uq_classfeeamounts_class_feetype_year";
    private const string ClassFeeClassIndex = "ix_classfeeamounts_classid";
    private const string FeePaymentStudentIndex = "ix_feepayments_studentid";

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeeTypes).Exists())
        {
            Create.Table(DatabaseConfig.TableFeeTypes).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("name").AsString(200).NotNullable()
                .WithColumn("category").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("frequency").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("ismandatory").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("isrefundable").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeeSettings).Exists())
        {
            Create.Table(DatabaseConfig.TableFeeSettings).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("paymentcycle").AsInt16().NotNullable().WithDefaultValue(2)
                .WithColumn("latefeeday").AsDecimal(10, 2).NotNullable().WithDefaultValue(0)
                .WithColumn("defaultacademicyearid").AsGuid().Nullable()
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Exists())
        {
            Create.Table(DatabaseConfig.TableClassFeeAmounts).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classid").AsGuid().NotNullable()
                .WithColumn("feetypeid").AsGuid().NotNullable()
                .WithColumn("academicyearid").AsGuid().NotNullable()
                .WithColumn("amount").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeePayments).Exists())
        {
            Create.Table(DatabaseConfig.TableFeePayments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable()
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
                .WithColumn("feetypeid").AsGuid().NotNullable()
                .WithColumn("amount").AsDecimal(12, 2).NotNullable()
                .WithAuditColumns();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Constraint(ClassFeeAmountUnique).Exists())
        {
            Create.UniqueConstraint(ClassFeeAmountUnique)
                .OnTable(DatabaseConfig.TableClassFeeAmounts).WithSchema(S)
                .Columns("classid", "feetypeid", "academicyearid");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassFeeAmounts).Index(ClassFeeClassIndex).Exists())
        {
            Create.Index(ClassFeeClassIndex)
                .OnTable(DatabaseConfig.TableClassFeeAmounts).InSchema(S)
                .OnColumn("classid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableFeePayments).Index(FeePaymentStudentIndex).Exists())
        {
            Create.Index(FeePaymentStudentIndex)
                .OnTable(DatabaseConfig.TableFeePayments).InSchema(S)
                .OnColumn("studentid").Ascending();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableFeePaymentAllocations).InSchema(S);
        Delete.Table(DatabaseConfig.TableFeePayments).InSchema(S);
        Delete.Table(DatabaseConfig.TableClassFeeAmounts).InSchema(S);
        Delete.Table(DatabaseConfig.TableFeeSettings).InSchema(S);
        Delete.Table(DatabaseConfig.TableFeeTypes).InSchema(S);
    }
}
