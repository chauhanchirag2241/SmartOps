using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

/// <summary>
/// Student admission fee selections (optional heads + custom amount) and per-student installments.
/// Single migration — no follow-up alter migration required.
/// </summary>
[Tags("School")]
[Migration(123, "School template — student fee assignments and installments at admission")]
public sealed class S123_StudentFeeHeadAssignments : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    private const string AssignmentsUnique = "uq_studentfeeheadassignments_student_feetype_version";
    private const string AssignmentsIndex = "ix_studentfeeheadassignments_student_version";
    private const string InstallmentsUnique = "uq_studentfeeinstallments_student_feetype_version_period";
    private const string InstallmentsIndex = "ix_studentfeeinstallments_student_version";

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeHeadAssignments).Exists())
        {
            Create.Table(DatabaseConfig.TableStudentFeeHeadAssignments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable()
                .WithColumn("feestructureversionid").AsGuid().NotNullable()
                .WithColumn("feetypeid").AsGuid().NotNullable()
                .WithColumn("isincluded").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("customannualamount").AsDecimal(12, 2).Nullable()
                .WithAuditColumns();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeHeadAssignments).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeHeadAssignments).Constraint(AssignmentsUnique).Exists())
        {
            Create.UniqueConstraint(AssignmentsUnique)
                .OnTable(DatabaseConfig.TableStudentFeeHeadAssignments).WithSchema(S)
                .Columns("studentid", "feetypeid", "feestructureversionid");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeHeadAssignments).Index(AssignmentsIndex).Exists())
        {
            Create.Index(AssignmentsIndex)
                .OnTable(DatabaseConfig.TableStudentFeeHeadAssignments).InSchema(S)
                .OnColumn("studentid").Ascending()
                .OnColumn("feestructureversionid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeInstallments).Exists())
        {
            Create.Table(DatabaseConfig.TableStudentFeeInstallments).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("studentid").AsGuid().NotNullable()
                .WithColumn("feestructureversionid").AsGuid().NotNullable()
                .WithColumn("classfeeinstallmentid").AsGuid().Nullable()
                .WithColumn("feetypeid").AsGuid().NotNullable()
                .WithColumn("periodindex").AsInt32().NotNullable()
                .WithColumn("periodlabel").AsString(100).NotNullable()
                .WithColumn("periodstart").AsDate().NotNullable()
                .WithColumn("periodend").AsDate().NotNullable()
                .WithColumn("amount").AsDecimal(12, 2).NotNullable().WithDefaultValue(0)
                .WithAuditColumns();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeInstallments).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeInstallments).Constraint(InstallmentsUnique).Exists())
        {
            Create.UniqueConstraint(InstallmentsUnique)
                .OnTable(DatabaseConfig.TableStudentFeeInstallments).WithSchema(S)
                .Columns("studentid", "feetypeid", "feestructureversionid", "periodindex");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeInstallments).Index(InstallmentsIndex).Exists())
        {
            Create.Index(InstallmentsIndex)
                .OnTable(DatabaseConfig.TableStudentFeeInstallments).InSchema(S)
                .OnColumn("studentid").Ascending()
                .OnColumn("feestructureversionid").Ascending();
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeInstallments).Index(InstallmentsIndex).Exists())
        {
            Delete.Index(InstallmentsIndex).OnTable(DatabaseConfig.TableStudentFeeInstallments).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeInstallments).Constraint(InstallmentsUnique).Exists())
        {
            Delete.UniqueConstraint(InstallmentsUnique).FromTable(DatabaseConfig.TableStudentFeeInstallments).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeInstallments).Exists())
        {
            Delete.Table(DatabaseConfig.TableStudentFeeInstallments).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeHeadAssignments).Index(AssignmentsIndex).Exists())
        {
            Delete.Index(AssignmentsIndex).OnTable(DatabaseConfig.TableStudentFeeHeadAssignments).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeHeadAssignments).Constraint(AssignmentsUnique).Exists())
        {
            Delete.UniqueConstraint(AssignmentsUnique).FromTable(DatabaseConfig.TableStudentFeeHeadAssignments).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableStudentFeeHeadAssignments).Exists())
        {
            Delete.Table(DatabaseConfig.TableStudentFeeHeadAssignments).InSchema(S);
        }
    }
}
