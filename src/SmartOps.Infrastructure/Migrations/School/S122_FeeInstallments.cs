using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(122, "School template — fee installments and amount basis")]
public sealed class S122_FeeInstallments : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string InstallmentUnique = "uq_classfeeinstallments_class_feetype_version_period";
    private const string InstallmentClassIndex = "ix_classfeeinstallments_class_version";
    private const string AllocationInstallmentIndex = "ix_feepaymentallocations_installmentid";

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableFeeTypes).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableFeeTypes).Column("amountbasis").Exists())
        {
            Alter.Table(DatabaseConfig.TableFeeTypes).InSchema(S)
                .AddColumn("amountbasis").AsInt16().NotNullable().WithDefaultValue(0);
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassFeeInstallments).Exists())
        {
            Create.Table(DatabaseConfig.TableClassFeeInstallments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("feestructureversionid").AsGuid().NotNullable()
                .WithColumn("classid").AsGuid().NotNullable()
                .WithColumn("feetypeid").AsGuid().NotNullable()
                .WithColumn("academicyearid").AsGuid().NotNullable()
                .WithColumn("periodindex").AsInt32().NotNullable()
                .WithColumn("periodlabel").AsString(100).NotNullable()
                .WithColumn("periodstart").AsDate().NotNullable()
                .WithColumn("periodend").AsDate().NotNullable()
                .WithColumn("amount").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
                .WithAuditColumns();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableClassFeeInstallments).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableClassFeeInstallments).Constraint(InstallmentUnique).Exists())
        {
            Create.UniqueConstraint(InstallmentUnique)
                .OnTable(DatabaseConfig.TableClassFeeInstallments).WithSchema(S)
                .Columns("classid", "feetypeid", "feestructureversionid", "periodindex");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassFeeInstallments).Index(InstallmentClassIndex).Exists())
        {
            Create.Index(InstallmentClassIndex)
                .OnTable(DatabaseConfig.TableClassFeeInstallments).InSchema(S)
                .OnColumn("classid").Ascending()
                .OnColumn("feestructureversionid").Ascending();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableFeePaymentAllocations).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableFeePaymentAllocations).Column("installmentid").Exists())
        {
            Alter.Table(DatabaseConfig.TableFeePaymentAllocations).InSchema(S)
                .AddColumn("installmentid").AsGuid().Nullable();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableFeePaymentAllocations).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableFeePaymentAllocations).Index(AllocationInstallmentIndex).Exists())
        {
            Create.Index(AllocationInstallmentIndex)
                .OnTable(DatabaseConfig.TableFeePaymentAllocations).InSchema(S)
                .OnColumn("installmentid").Ascending();
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableFeePaymentAllocations).Index(AllocationInstallmentIndex).Exists())
        {
            Delete.Index(AllocationInstallmentIndex).OnTable(DatabaseConfig.TableFeePaymentAllocations).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableFeePaymentAllocations).Column("installmentid").Exists())
        {
            Delete.Column("installmentid").FromTable(DatabaseConfig.TableFeePaymentAllocations).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableClassFeeInstallments).Exists())
        {
            Delete.Table(DatabaseConfig.TableClassFeeInstallments).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableFeeTypes).Column("amountbasis").Exists())
        {
            Delete.Column("amountbasis").FromTable(DatabaseConfig.TableFeeTypes).InSchema(S);
        }
    }
}
