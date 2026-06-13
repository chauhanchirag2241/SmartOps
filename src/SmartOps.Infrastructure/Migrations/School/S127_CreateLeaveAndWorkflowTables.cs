using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(127, "School template — leave and workflow tables")]
public sealed class S127_CreateLeaveAndWorkflowTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableLeaveRequests).Exists())
        {
            Create.Table(DatabaseConfig.TableLeaveRequests).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("requesttype").AsInt16().NotNullable()
                .WithColumn("employeeid").AsGuid().Nullable()
                .WithColumn("studentid").AsGuid().Nullable()
                .WithColumn("requestedbyuserid").AsGuid().NotNullable()
                .WithColumn("fromdate").AsDate().NotNullable()
                .WithColumn("todate").AsDate().NotNullable()
                .WithColumn("leavetype").AsInt16().Nullable()
                .WithColumn("reason").AsString(int.MaxValue).Nullable()
                .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("approvedbyuserid").AsGuid().Nullable()
                .WithColumn("approvedon").AsDateTimeOffset().Nullable()
                .WithColumn("approverremark").AsString(1000).Nullable()
                .WithAuditColumns();

            Create.Index("ix_leaverequests_status_fromdate")
                .OnTable(DatabaseConfig.TableLeaveRequests).InSchema(S)
                .OnColumn("status").Ascending()
                .OnColumn("fromdate").Ascending();

            Create.Index("ix_leaverequests_studentid")
                .OnTable(DatabaseConfig.TableLeaveRequests).InSchema(S)
                .OnColumn("studentid").Ascending();

            Create.Index("ix_leaverequests_employeeid")
                .OnTable(DatabaseConfig.TableLeaveRequests).InSchema(S)
                .OnColumn("employeeid").Ascending();

            Create.Index("ix_leaverequests_requestedby")
                .OnTable(DatabaseConfig.TableLeaveRequests).InSchema(S)
                .OnColumn("requestedbyuserid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableWorkflowItems).Exists())
        {
            Create.Table(DatabaseConfig.TableWorkflowItems).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("assigneeuserid").AsGuid().NotNullable()
                .WithColumn("itemtype").AsInt16().NotNullable()
                .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("referencetype").AsInt16().NotNullable()
                .WithColumn("referenceid").AsGuid().NotNullable()
                .WithColumn("title").AsString(300).NotNullable()
                .WithColumn("summary").AsString(1000).Nullable()
                .WithColumn("duedate").AsDate().Nullable()
                .WithColumn("priority").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("payloadjson").AsString(int.MaxValue).Nullable()
                .WithColumn("completedbyuserid").AsGuid().Nullable()
                .WithColumn("completedon").AsDateTimeOffset().Nullable()
                .WithColumn("outcome").AsString(100).Nullable()
                .WithAuditColumns();

            Create.Index("ix_workflowitems_assignee_status")
                .OnTable(DatabaseConfig.TableWorkflowItems).InSchema(S)
                .OnColumn("assigneeuserid").Ascending()
                .OnColumn("status").Ascending();

            Create.Index("ix_workflowitems_reference")
                .OnTable(DatabaseConfig.TableWorkflowItems).InSchema(S)
                .OnColumn("referencetype").Ascending()
                .OnColumn("referenceid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableWorkflowItemActions).Exists())
        {
            Create.Table(DatabaseConfig.TableWorkflowItemActions).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("workflowitemid").AsGuid().NotNullable()
                .WithColumn("actioncode").AsString(50).NotNullable()
                .WithColumn("comment").AsString(2000).Nullable()
                .WithColumn("actoruserid").AsGuid().NotNullable()
                .WithColumn("actedon").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
                .WithColumn("metadatajson").AsString(int.MaxValue).Nullable()
                .WithAuditColumns();

            Create.Index("ix_workflowitemactions_itemid")
                .OnTable(DatabaseConfig.TableWorkflowItemActions).InSchema(S)
                .OnColumn("workflowitemid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableNotices).Exists())
        {
            Create.Table(DatabaseConfig.TableNotices).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("title").AsString(300).NotNullable()
                .WithColumn("body").AsString(int.MaxValue).NotNullable()
                .WithColumn("createdbyuserid").AsGuid().NotNullable()
                .WithColumn("publishedon").AsDateTimeOffset().Nullable()
                .WithColumn("requiresresponse").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("responsedeadline").AsDate().Nullable()
                .WithColumn("targettype").AsInt16().NotNullable().WithDefaultValue(1)
                .WithColumn("targetrefid").AsGuid().Nullable()
                .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("contenttype").AsInt16().NotNullable().WithDefaultValue(1)
                .WithColumn("contentjson").AsString(int.MaxValue).Nullable()
                .WithAuditColumns();

            Create.Index("ix_notices_status")
                .OnTable(DatabaseConfig.TableNotices).InSchema(S)
                .OnColumn("status").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableNoticeResponses).Exists())
        {
            Create.Table(DatabaseConfig.TableNoticeResponses).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("noticeid").AsGuid().NotNullable()
                .WithColumn("respondentuserid").AsGuid().NotNullable()
                .WithColumn("responsebody").AsString(int.MaxValue).NotNullable()
                .WithColumn("respondedon").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentDateTime)
                .WithAuditColumns();

            Create.UniqueConstraint("uq_noticeresponses_notice_user")
                .OnTable(DatabaseConfig.TableNoticeResponses).WithSchema(S)
                .Columns("noticeid", "respondentuserid");
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableNoticeResponses).InSchema(S);
        Delete.Table(DatabaseConfig.TableNotices).InSchema(S);
        Delete.Table(DatabaseConfig.TableWorkflowItemActions).InSchema(S);
        Delete.Table(DatabaseConfig.TableWorkflowItems).InSchema(S);
        Delete.Table(DatabaseConfig.TableLeaveRequests).InSchema(S);
    }
}
