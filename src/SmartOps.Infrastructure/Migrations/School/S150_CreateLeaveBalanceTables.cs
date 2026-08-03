using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.Leave;
using SmartOps.Infrastructure.Migrations.Extensions;

using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(150, "School — leave types, policies, balances, ledger, accrual runs")]
public sealed class S150_CreateLeaveBalanceTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid CasualId = LeaveTypeSeedIds.CasualLeave;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableLeaveTypes).Exists())
        {
            Create.Table(DatabaseConfig.TableLeaveTypes).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable()
                .WithColumn("code").AsString(20).NotNullable()
                .WithColumn("name").AsString(100).NotNullable()
                .WithColumn("ispaid").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("requiresbalance").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("allowhalfday").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("carryforward").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("sortorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Create.UniqueConstraint("uq_leavetypes_code")
                .OnTable(DatabaseConfig.TableLeaveTypes).WithSchema(S)
                .Column("code");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableLeavePolicies).Exists())
        {
            Create.Table(DatabaseConfig.TableLeavePolicies).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("usertypeid").AsGuid().NotNullable()
                .WithColumn("leavetypeid").AsGuid().NotNullable()
                .WithColumn("monthlyleave").AsDecimal(6, 2).NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Create.UniqueConstraint("uq_leavepolicies_usertype_leavetype")
                .OnTable(DatabaseConfig.TableLeavePolicies).WithSchema(S)
                .Columns("usertypeid", "leavetypeid");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableLeaveBalances).Exists())
        {
            Create.Table(DatabaseConfig.TableLeaveBalances).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("employeeid").AsGuid().NotNullable()
                .WithColumn("leavetypeid").AsGuid().NotNullable()
                .WithColumn("academicyearid").AsGuid().NotNullable()
                .WithColumn("openingbalance").AsDecimal(8, 2).NotNullable().WithDefaultValue(0)
                .WithColumn("accrued").AsDecimal(8, 2).NotNullable().WithDefaultValue(0)
                .WithColumn("used").AsDecimal(8, 2).NotNullable().WithDefaultValue(0)
                .WithColumn("adjusted").AsDecimal(8, 2).NotNullable().WithDefaultValue(0)
                .WithColumn("closingbalance").AsDecimal(8, 2).NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Create.UniqueConstraint("uq_leavebalances_emp_type_year")
                .OnTable(DatabaseConfig.TableLeaveBalances).WithSchema(S)
                .Columns("employeeid", "leavetypeid", "academicyearid");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableLeaveLedger).Exists())
        {
            Create.Table(DatabaseConfig.TableLeaveLedger).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("employeeid").AsGuid().NotNullable()
                .WithColumn("leavetypeid").AsGuid().NotNullable()
                .WithColumn("academicyearid").AsGuid().NotNullable()
                .WithColumn("txntype").AsInt16().NotNullable()
                .WithColumn("days").AsDecimal(8, 2).NotNullable()
                .WithColumn("balanceafter").AsDecimal(8, 2).NotNullable()
                .WithColumn("referenceid").AsGuid().Nullable()
                .WithColumn("remark").AsString(int.MaxValue).Nullable()
                .WithColumn("txndate").AsDate().NotNullable()
                .WithColumn("createdby").AsGuid().NotNullable().WithDefaultValue(SeedActor)
                .WithColumn("createdon").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime);

            Create.Index("ix_leaveledger_employee_type")
                .OnTable(DatabaseConfig.TableLeaveLedger).InSchema(S)
                .OnColumn("employeeid").Ascending()
                .OnColumn("leavetypeid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableLeaveAccrualRuns).Exists())
        {
            Create.Table(DatabaseConfig.TableLeaveAccrualRuns).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("year").AsInt32().NotNullable()
                .WithColumn("month").AsInt32().NotNullable()
                .WithColumn("ranon").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
                .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("employeesscored").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("errorlog").AsString(int.MaxValue).Nullable();

            Create.UniqueConstraint("uq_leaveaccrualruns_year_month")
                .OnTable(DatabaseConfig.TableLeaveAccrualRuns).WithSchema(S)
                .Columns("year", "month");
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableLeaveRequests).Exists())
        {
            if (!Schema.Schema(S).Table(DatabaseConfig.TableLeaveRequests).Column("leavetypeid").Exists())
            {
                Alter.Table(DatabaseConfig.TableLeaveRequests).InSchema(S)
                    .AddColumn("leavetypeid").AsGuid().Nullable();
            }

            if (!Schema.Schema(S).Table(DatabaseConfig.TableLeaveRequests).Column("totaldays").Exists())
            {
                Alter.Table(DatabaseConfig.TableLeaveRequests).InSchema(S)
                    .AddColumn("totaldays").AsDecimal(6, 2).NotNullable().WithDefaultValue(0);
            }

            if (!Schema.Schema(S).Table(DatabaseConfig.TableLeaveRequests).Column("ishalfday").Exists())
            {
                Alter.Table(DatabaseConfig.TableLeaveRequests).InSchema(S)
                    .AddColumn("ishalfday").AsBoolean().NotNullable().WithDefaultValue(false);
            }

            if (!Schema.Schema(S).Table(DatabaseConfig.TableLeaveRequests).Column("deductedfrombalance").Exists())
            {
                Alter.Table(DatabaseConfig.TableLeaveRequests).InSchema(S)
                    .AddColumn("deductedfrombalance").AsBoolean().NotNullable().WithDefaultValue(false);
            }
        }

        DateTimeOffset now = SchoolLocalTime.Now();
        Execute.Sql($"""
INSERT INTO {S}.{DatabaseConfig.TableLeaveTypes}
    (id, code, name, ispaid, requiresbalance, allowhalfday, carryforward, sortorder,
     isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{CasualId}', 'CL', 'Casual Leave', true, true, true, true, 1,
       true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {S}.{DatabaseConfig.TableLeaveTypes} WHERE code = 'CL'
);
""");

        // Backfill leavetypeid for Casual enum = 1
        Execute.Sql($"""
UPDATE {S}.{DatabaseConfig.TableLeaveRequests}
SET leavetypeid = '{CasualId}'
WHERE leavetype = 1 AND leavetypeid IS NULL;
""");

        Execute.Sql($"""
UPDATE {S}.{DatabaseConfig.TableLeaveRequests}
SET totaldays = GREATEST(1, (todate - fromdate) + 1)
WHERE totaldays = 0;
""");

        // Seed CL = 1/month for staff user types
        string[] userTypeIds =
        [
            UserTypeCodes.Ids.Teacher.ToString(),
            UserTypeCodes.Ids.Accountant.ToString(),
            UserTypeCodes.Ids.NonAcademicStaff.ToString(),
            UserTypeCodes.Ids.OfficeStaff.ToString(),
            UserTypeCodes.Ids.FrontOfficeExecutive.ToString(),
            UserTypeCodes.Ids.Principal.ToString(),
        ];

        foreach (string ut in userTypeIds)
        {
            Execute.Sql($"""
INSERT INTO {S}.{DatabaseConfig.TableLeavePolicies}
    (id, usertypeid, leavetypeid, monthlyleave, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), '{ut}', '{CasualId}', 1.00, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {S}.{DatabaseConfig.TableLeavePolicies}
    WHERE usertypeid = '{ut}' AND leavetypeid = '{CasualId}'
);
""");
        }
    }

    public override void Down()
    {
        // Keep data; no destructive down for production safety.
    }
}
