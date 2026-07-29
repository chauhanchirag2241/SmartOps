using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(122, "School template — fee installments")]
public sealed class S122_FeeInstallments : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string InstallmentUnique = "uq_classfeeinstallments_classgroup_feehead_structure_period";
    private const string InstallmentClassIndex = "ix_classfeeinstallments_classgroup_version";
    private const string AllocationInstallmentIndex = "ix_feepaymentallocations_installmentid";

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassFeeInstallments).Exists())
        {
            Create.Table(DatabaseConfig.TableClassFeeInstallments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("feestructureid").AsGuid().NotNullable()
                .WithColumn("classgroupid").AsGuid().NotNullable()
                .WithColumn("feeheadid").AsGuid().NotNullable()
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
                .Columns("classgroupid", "feeheadid", "feestructureid", "periodindex");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableClassFeeInstallments).Index(InstallmentClassIndex).Exists())
        {
            Create.Index(InstallmentClassIndex)
                .OnTable(DatabaseConfig.TableClassFeeInstallments).InSchema(S)
                .OnColumn("classgroupid").Ascending()
                .OnColumn("feestructureid").Ascending();
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
    }
}
