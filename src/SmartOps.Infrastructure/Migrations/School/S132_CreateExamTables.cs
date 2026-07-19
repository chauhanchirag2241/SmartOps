using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(132, "School template — exam management tables")]
public sealed class S132_CreateExamTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;

    private const string GradeScaleDetailScaleIndex = "ix_examgradescaledetails_scaleid";
    private const string ExamGroupYearIndex = "ix_examgroups_academicyearid";
    private const string ExamGroupIndex = "ix_exams_examgroupid";
    private const string ExamClassUnique = "uq_examclasses_exam_class";
    private const string ComponentExamIndex = "ix_exammarkcomponents_examid";
    private const string ScheduleExamIndex = "ix_examschedules_examid";
    private const string ScheduleUnique = "uq_examschedules_exam_class_subject";
    private const string MarkUnique = "uq_examstudentmarks_schedule_component_student";
    private const string MarkScheduleIndex = "ix_examstudentmarks_scheduleid";
    private const string ResultUnique = "uq_examresults_exam_student";
    private const string HallTicketUnique = "uq_examhalltickets_exam_student";

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableExamGradeScales).Exists())
        {
            Create.Table(DatabaseConfig.TableExamGradeScales).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("name").AsString(200).NotNullable()
                .WithColumn("description").AsString(500).Nullable()
                .WithColumn("isdefault").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithAuditColumns();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableExamGradeScaleDetails).Exists())
        {
            Create.Table(DatabaseConfig.TableExamGradeScaleDetails).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("gradescaleid").AsGuid().NotNullable()
                .WithColumn("grade").AsString(20).NotNullable()
                .WithColumn("minpercent").AsDecimal(6, 2).NotNullable()
                .WithColumn("maxpercent").AsDecimal(6, 2).NotNullable()
                .WithColumn("gradepoint").AsDecimal(6, 2).Nullable()
                .WithColumn("description").AsString(300).Nullable()
                .WithColumn("displayorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Create.Index(GradeScaleDetailScaleIndex)
                .OnTable(DatabaseConfig.TableExamGradeScaleDetails).InSchema(S)
                .OnColumn("gradescaleid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableExamGroups).Exists())
        {
            Create.Table(DatabaseConfig.TableExamGroups).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("academicyearid").AsGuid().NotNullable()
                .WithColumn("name").AsString(300).NotNullable()
                .WithColumn("description").AsString(1000).Nullable()
                .WithColumn("gradescaleid").AsGuid().Nullable()
                .WithColumn("evaluationtype").AsInt16().NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Create.Index(ExamGroupYearIndex)
                .OnTable(DatabaseConfig.TableExamGroups).InSchema(S)
                .OnColumn("academicyearid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableExams).Exists())
        {
            Create.Table(DatabaseConfig.TableExams).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("examgroupid").AsGuid().NotNullable()
                .WithColumn("branchid").AsGuid().NotNullable()
                .WithColumn("academicyearid").AsGuid().NotNullable()
                .WithColumn("name").AsString(300).NotNullable()
                .WithColumn("examtype").AsString(100).NotNullable()
                .WithColumn("academicperiodid").AsGuid().Nullable()
                .WithColumn("startdate").AsDate().NotNullable()
                .WithColumn("enddate").AsDate().NotNullable()
                .WithColumn("minpasspercent").AsDecimal(6, 2).NotNullable().WithDefaultValue(33)
                .WithColumn("gradescaleid").AsGuid().Nullable()
                .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("resultdeclared").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("resultdeclaredon").AsDateTime().Nullable()
                .WithColumn("resultdeclaredby").AsGuid().Nullable()
                .WithColumn("description").AsString(int.MaxValue).Nullable()
                .WithAuditColumns();

            Create.Index(ExamGroupIndex)
                .OnTable(DatabaseConfig.TableExams).InSchema(S)
                .OnColumn("examgroupid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableExamClasses).Exists())
        {
            Create.Table(DatabaseConfig.TableExamClasses).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("examid").AsGuid().NotNullable()
                .WithColumn("classid").AsGuid().NotNullable()
                .WithAuditColumns();

            Create.UniqueConstraint(ExamClassUnique)
                .OnTable(DatabaseConfig.TableExamClasses).WithSchema(S)
                .Columns("examid", "classid");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableExamMarkComponents).Exists())
        {
            Create.Table(DatabaseConfig.TableExamMarkComponents).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("examid").AsGuid().NotNullable()
                .WithColumn("name").AsString(100).NotNullable()
                .WithColumn("maxmarks").AsDecimal(8, 2).NotNullable()
                .WithColumn("passingmarks").AsDecimal(8, 2).Nullable()
                .WithColumn("displayorder").AsInt32().NotNullable().WithDefaultValue(0)
                .WithAuditColumns();

            Create.Index(ComponentExamIndex)
                .OnTable(DatabaseConfig.TableExamMarkComponents).InSchema(S)
                .OnColumn("examid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableExamSchedules).Exists())
        {
            Create.Table(DatabaseConfig.TableExamSchedules).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("examid").AsGuid().NotNullable()
                .WithColumn("classid").AsGuid().NotNullable()
                .WithColumn("subjectid").AsGuid().NotNullable()
                .WithColumn("examdate").AsDate().NotNullable()
                .WithColumn("starttime").AsString(10).Nullable()
                .WithColumn("endtime").AsString(10).Nullable()
                .WithColumn("roomno").AsString(100).Nullable()
                .WithColumn("invigilatorid").AsGuid().Nullable()
                .WithAuditColumns();

            Create.Index(ScheduleExamIndex)
                .OnTable(DatabaseConfig.TableExamSchedules).InSchema(S)
                .OnColumn("examid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableExamStudentMarks).Exists())
        {
            Create.Table(DatabaseConfig.TableExamStudentMarks).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("examscheduleid").AsGuid().NotNullable()
                .WithColumn("componentid").AsGuid().NotNullable()
                .WithColumn("studentid").AsGuid().NotNullable()
                .WithColumn("marksobtained").AsDecimal(8, 2).Nullable()
                .WithColumn("isabsent").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("isexempted").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("remark").AsString(500).Nullable()
                .WithAuditColumns();

            Create.UniqueConstraint(MarkUnique)
                .OnTable(DatabaseConfig.TableExamStudentMarks).WithSchema(S)
                .Columns("examscheduleid", "componentid", "studentid");

            Create.Index(MarkScheduleIndex)
                .OnTable(DatabaseConfig.TableExamStudentMarks).InSchema(S)
                .OnColumn("examscheduleid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableExamResults).Exists())
        {
            Create.Table(DatabaseConfig.TableExamResults).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("examid").AsGuid().NotNullable()
                .WithColumn("classid").AsGuid().NotNullable()
                .WithColumn("studentid").AsGuid().NotNullable()
                .WithColumn("totalmarks").AsDecimal(10, 2).NotNullable().WithDefaultValue(0)
                .WithColumn("maxmarks").AsDecimal(10, 2).NotNullable().WithDefaultValue(0)
                .WithColumn("percentage").AsDecimal(6, 2).NotNullable().WithDefaultValue(0)
                .WithColumn("grade").AsString(20).Nullable()
                .WithColumn("rank").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("result").AsInt16().NotNullable().WithDefaultValue(0)
                .WithColumn("subjectresults").AsString(int.MaxValue).Nullable()
                .WithColumn("declaredon").AsDateTime().Nullable()
                .WithColumn("declaredby").AsGuid().Nullable()
                .WithAuditColumns();

            Create.UniqueConstraint(ResultUnique)
                .OnTable(DatabaseConfig.TableExamResults).WithSchema(S)
                .Columns("examid", "studentid");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableExamHallTickets).Exists())
        {
            Create.Table(DatabaseConfig.TableExamHallTickets).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("examid").AsGuid().NotNullable()
                .WithColumn("classid").AsGuid().NotNullable()
                .WithColumn("studentid").AsGuid().NotNullable()
                .WithColumn("ticketno").AsString(50).NotNullable()
                .WithColumn("seatno").AsString(50).Nullable()
                .WithAuditColumns();

            Create.UniqueConstraint(HallTicketUnique)
                .OnTable(DatabaseConfig.TableExamHallTickets).WithSchema(S)
                .Columns("examid", "studentid");
        }
    }

    public override void Down()
    {
        Delete.Table(DatabaseConfig.TableExamHallTickets).InSchema(S);
        Delete.Table(DatabaseConfig.TableExamResults).InSchema(S);
        Delete.Table(DatabaseConfig.TableExamStudentMarks).InSchema(S);
        Delete.Table(DatabaseConfig.TableExamSchedules).InSchema(S);
        Delete.Table(DatabaseConfig.TableExamMarkComponents).InSchema(S);
        Delete.Table(DatabaseConfig.TableExamClasses).InSchema(S);
        Delete.Table(DatabaseConfig.TableExams).InSchema(S);
        Delete.Table(DatabaseConfig.TableExamGroups).InSchema(S);
        Delete.Table(DatabaseConfig.TableExamGradeScaleDetails).InSchema(S);
        Delete.Table(DatabaseConfig.TableExamGradeScales).InSchema(S);
    }
}
