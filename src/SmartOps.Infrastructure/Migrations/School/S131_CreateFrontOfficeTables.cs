using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(131, "School template — front office tables")]
public sealed class S131_CreateFrontOfficeTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private static readonly (Guid Id, string Name, string Desc, int Order)[] ComplaintTypes =
    [
        (Guid.Parse("51000000-0000-0000-0000-000000000001"), "Infrastructure Issue", "Building, furniture, water, electricity problems", 1),
        (Guid.Parse("51000000-0000-0000-0000-000000000002"), "Staff Behavior", "Complaints regarding teacher or staff conduct", 2),
        (Guid.Parse("51000000-0000-0000-0000-000000000003"), "Academic Issue", "Curriculum, homework, exam-related complaints", 3),
        (Guid.Parse("51000000-0000-0000-0000-000000000004"), "Fee Related", "Fee overcharge, receipt issues, payment disputes", 4),
        (Guid.Parse("51000000-0000-0000-0000-000000000005"), "Transport Issue", "Bus timing, driver behavior, route problems", 5),
        (Guid.Parse("51000000-0000-0000-0000-000000000006"), "Cleanliness / Hygiene", "Washroom, canteen, campus cleanliness", 6),
    ];

    private static readonly (Guid Id, string Name, string Desc, int Order)[] VisitorPurposes =
    [
        (Guid.Parse("52000000-0000-0000-0000-000000000001"), "Meeting with Teacher", "Parent meeting with class or subject teacher", 1),
        (Guid.Parse("52000000-0000-0000-0000-000000000002"), "Fee Payment", "Paying school fees or getting receipt", 2),
        (Guid.Parse("52000000-0000-0000-0000-000000000003"), "Document Submission", "Submitting required documents", 3),
        (Guid.Parse("52000000-0000-0000-0000-000000000004"), "Admission Inquiry", "Inquiring about admission for new student", 4),
        (Guid.Parse("52000000-0000-0000-0000-000000000005"), "Collect TC/Certificate", "Collecting transfer certificate or other docs", 5),
        (Guid.Parse("52000000-0000-0000-0000-000000000006"), "Official Visit", "Government official, inspector or auditor", 6),
        (Guid.Parse("52000000-0000-0000-0000-000000000007"), "Personal Visit", "Personal or unofficial visit", 7),
    ];

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableComplaintTypes).Exists())
        {
            Create.Table(DatabaseConfig.TableComplaintTypes).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("name").AsString(200).NotNullable()
                .WithColumn("description").AsString(500).Nullable()
                .WithColumn("displayorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Create.Index("ix_complainttypes_displayorder")
                .OnTable(DatabaseConfig.TableComplaintTypes).InSchema(S)
                .OnColumn("displayorder").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableVisitorPurposes).Exists())
        {
            Create.Table(DatabaseConfig.TableVisitorPurposes).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("name").AsString(200).NotNullable()
                .WithColumn("description").AsString(500).Nullable()
                .WithColumn("displayorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Create.Index("ix_visitorpurposes_displayorder")
                .OnTable(DatabaseConfig.TableVisitorPurposes).InSchema(S)
                .OnColumn("displayorder").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableVisitors).Exists())
        {
            Create.Table(DatabaseConfig.TableVisitors).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
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
                .WithColumn("callername").AsString(200).NotNullable()
                .WithColumn("phone").AsString(50).Nullable()
                .WithColumn("calltype").AsInt16().NotNullable()
                .WithColumn("calldate").AsDate().NotNullable()
                .WithColumn("duration").AsString(50).Nullable()
                .WithColumn("description").AsString(int.MaxValue).NotNullable()
                .WithColumn("nextfollowupdate").AsDate().Nullable()
                .WithColumn("note").AsString(1000).Nullable()
                .WithAuditColumns();

            Create.Index("ix_phonelogs_calldate")
                .OnTable(DatabaseConfig.TablePhoneLogs).InSchema(S)
                .OnColumn("calldate").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableComplaints).Exists())
        {
            Create.Table(DatabaseConfig.TableComplaints).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
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

        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((Guid id, string name, string desc, int order) in ComplaintTypes)
        {
            string escapedDesc = desc.Replace("'", "''");
            Execute.Sql($"""
INSERT INTO {S}.{DatabaseConfig.TableComplaintTypes}
    (id, name, description, displayorder, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name.Replace("'", "''")}', '{escapedDesc}', {order}, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (SELECT 1 FROM {S}.{DatabaseConfig.TableComplaintTypes} WHERE id = '{id}');
""");
        }

        foreach ((Guid id, string name, string desc, int order) in VisitorPurposes)
        {
            string escapedDesc = desc.Replace("'", "''");
            Execute.Sql($"""
INSERT INTO {S}.{DatabaseConfig.TableVisitorPurposes}
    (id, name, description, displayorder, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name.Replace("'", "''")}', '{escapedDesc}', {order}, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (SELECT 1 FROM {S}.{DatabaseConfig.TableVisitorPurposes} WHERE id = '{id}');
""");
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
