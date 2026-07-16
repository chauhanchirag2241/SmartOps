using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(131, "School template — front office tables")]
public sealed class S131_CreateFrontOfficeTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static string G => DatabaseConfig.Schema_Global;

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableComplaintTypes).Exists())
        {
            Create.Table(DatabaseConfig.TableComplaintTypes).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("name").AsString(200).NotNullable()
                .WithColumn("description").AsString(500).Nullable()
                .WithColumn("displayorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableComplaintTypes}
    ADD CONSTRAINT fk_complainttypes_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE INDEX ix_complainttypes_branchid ON {S}.{DatabaseConfig.TableComplaintTypes} (branchid);
CREATE INDEX ix_complainttypes_displayorder ON {S}.{DatabaseConfig.TableComplaintTypes} (displayorder);
""");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableVisitorPurposes).Exists())
        {
            Create.Table(DatabaseConfig.TableVisitorPurposes).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("name").AsString(200).NotNullable()
                .WithColumn("description").AsString(500).Nullable()
                .WithColumn("displayorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableVisitorPurposes}
    ADD CONSTRAINT fk_visitorpurposes_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE INDEX ix_visitorpurposes_branchid ON {S}.{DatabaseConfig.TableVisitorPurposes} (branchid);
CREATE INDEX ix_visitorpurposes_displayorder ON {S}.{DatabaseConfig.TableVisitorPurposes} (displayorder);
""");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableVisitors).Exists())
        {
            Create.Table(DatabaseConfig.TableVisitors).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("name").AsString(200).NotNullable()
                .WithColumn("phone").AsString(50).Nullable()
                .WithColumn("idcardtype").AsString(100).Nullable()
                .WithColumn("idcardnumber").AsString(100).Nullable()
                .WithColumn("purposeid").AsGuid().NotNullable()
                    .ForeignKey("fk_visitors_purposeid", S, DatabaseConfig.TableVisitorPurposes, "id")
                .WithColumn("meetingwith").AsString(200).Nullable()
                .WithColumn("intime").AsDateTimeOffset().NotNullable()
                .WithColumn("outtime").AsDateTimeOffset().Nullable()
                .WithColumn("note").AsString(1000).Nullable()
                .WithColumn("documentpath").AsString(1000).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableVisitors}
    ADD CONSTRAINT fk_visitors_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE INDEX ix_visitors_branchid ON {S}.{DatabaseConfig.TableVisitors} (branchid);
""");

            Create.Index("ix_visitors_intime")
                .OnTable(DatabaseConfig.TableVisitors).InSchema(S)
                .OnColumn("intime").Ascending();

            Create.Index("ix_visitors_purposeid")
                .OnTable(DatabaseConfig.TableVisitors).InSchema(S)
                .OnColumn("purposeid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TablePhoneLogs).Exists())
        {
            Create.Table(DatabaseConfig.TablePhoneLogs).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("callername").AsString(200).NotNullable()
                .WithColumn("phone").AsString(50).Nullable()
                .WithColumn("calltype").AsInt16().NotNullable()
                .WithColumn("calldate").AsDate().NotNullable()
                .WithColumn("duration").AsString(50).Nullable()
                .WithColumn("description").AsString(int.MaxValue).NotNullable()
                .WithColumn("nextfollowupdate").AsDate().Nullable()
                .WithColumn("note").AsString(1000).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TablePhoneLogs}
    ADD CONSTRAINT fk_phonelogs_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE INDEX ix_phonelogs_branchid ON {S}.{DatabaseConfig.TablePhoneLogs} (branchid);
""");

            Create.Index("ix_phonelogs_calldate")
                .OnTable(DatabaseConfig.TablePhoneLogs).InSchema(S)
                .OnColumn("calldate").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableComplaints).Exists())
        {
            Create.Table(DatabaseConfig.TableComplaints).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("complainttypeid").AsGuid().NotNullable()
                    .ForeignKey("fk_complaints_complainttypeid", S, DatabaseConfig.TableComplaintTypes, "id")
                .WithColumn("complaintdate").AsDate().NotNullable()
                .WithColumn("isanonymous").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("complainantname").AsString(200).Nullable()
                .WithColumn("phone").AsString(50).Nullable()
                .WithColumn("description").AsString(int.MaxValue).NotNullable()
                .WithColumn("assignedtoemployeeid").AsGuid().NotNullable()
                .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("actiontaken").AsString(int.MaxValue).Nullable()
                .WithColumn("note").AsString(1000).Nullable()
                .WithColumn("documentpath").AsString(1000).Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableComplaints}
    ADD CONSTRAINT fk_complaints_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE INDEX ix_complaints_branchid ON {S}.{DatabaseConfig.TableComplaints} (branchid);
""");

            Create.Index("ix_complaints_complaintdate")
                .OnTable(DatabaseConfig.TableComplaints).InSchema(S)
                .OnColumn("complaintdate").Ascending();

            Create.Index("ix_complaints_complainttypeid")
                .OnTable(DatabaseConfig.TableComplaints).InSchema(S)
                .OnColumn("complainttypeid").Ascending();

            Create.Index("ix_complaints_status")
                .OnTable(DatabaseConfig.TableComplaints).InSchema(S)
                .OnColumn("status").Ascending();

            Create.Index("ix_complaints_assignedtoemployeeid")
                .OnTable(DatabaseConfig.TableComplaints).InSchema(S)
                .OnColumn("assignedtoemployeeid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableAdmissionInquiries).Exists())
        {
            Create.Table(DatabaseConfig.TableAdmissionInquiries).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("parentname").AsString(200).NotNullable()
                .WithColumn("phone").AsString(50).Nullable()
                .WithColumn("whatsapp").AsString(50).Nullable()
                .WithColumn("email").AsString(200).Nullable()
                .WithColumn("address").AsString(500).Nullable()
                .WithColumn("studentname").AsString(200).NotNullable()
                .WithColumn("classlabel").AsString(100).Nullable()
                .WithColumn("inquirydate").AsDate().NotNullable()
                .WithColumn("nextfollowupdate").AsDate().Nullable()
                .WithColumn("assignedtoemployeeid").AsGuid().Nullable()
                .WithColumn("reference").AsString(300).Nullable()
                .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("description").AsString(int.MaxValue).Nullable()
                .WithColumn("autofollowup").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("streamgroup").AsInt16().Nullable()
                .WithAuditColumns();

            Execute.Sql($"""
ALTER TABLE {S}.{DatabaseConfig.TableAdmissionInquiries}
    ADD CONSTRAINT fk_admissioninquiries_branchid FOREIGN KEY (branchid)
    REFERENCES {G}.{DatabaseConfig.TableSchoolBranches}(id);

CREATE INDEX ix_admissioninquiries_branchid ON {S}.{DatabaseConfig.TableAdmissionInquiries} (branchid);
""");

            Create.Index("ix_admissioninquiries_inquirydate")
                .OnTable(DatabaseConfig.TableAdmissionInquiries).InSchema(S)
                .OnColumn("inquirydate").Ascending();

            Create.Index("ix_admissioninquiries_status")
                .OnTable(DatabaseConfig.TableAdmissionInquiries).InSchema(S)
                .OnColumn("status").Ascending();

            Create.Index("ix_admissioninquiries_assignedtoemployeeid")
                .OnTable(DatabaseConfig.TableAdmissionInquiries).InSchema(S)
                .OnColumn("assignedtoemployeeid").Ascending();
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableAdmissionInquiries).InSchema(S);
        Delete.Table(DatabaseConfig.TableComplaints).InSchema(S);
        Delete.Table(DatabaseConfig.TablePhoneLogs).InSchema(S);
        Delete.Table(DatabaseConfig.TableVisitors).InSchema(S);
        Delete.Table(DatabaseConfig.TableVisitorPurposes).InSchema(S);
        Delete.Table(DatabaseConfig.TableComplaintTypes).InSchema(S);
    }
}
