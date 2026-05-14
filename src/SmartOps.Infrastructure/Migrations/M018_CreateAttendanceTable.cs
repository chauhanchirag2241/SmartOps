using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Migrations;

[Migration(018)]
public sealed class M018_CreateAttendanceTable : Migration
{
    private const string UniqueAttendanceConstraintName = "uq_attendance_class_student_date";
    private const string ClassDateIndexName = "ix_attendance_classdate";
    private const string StudentIndexName = "ix_attendance_student";

    public override void Up()
    {
        if (!Schema.Schema(DatabaseConfig.Schema_School).Exists())
        {
            Create.Schema(DatabaseConfig.Schema_School);
        }

        if (!Schema.Schema(DatabaseConfig.Schema_School).Table(DatabaseConfig.TableAttendance).Exists())
        {
            Create.Table(DatabaseConfig.TableAttendance).InSchema(DatabaseConfig.Schema_School)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classid").AsGuid().NotNullable()
                .WithColumn("studentid").AsGuid().NotNullable()
                .WithColumn("teacherid").AsGuid().NotNullable()
                .WithColumn("attendancedate").AsDate().NotNullable()
                .WithColumn("status").AsInt16().NotNullable()
                .WithColumn("remarks").AsString(int.MaxValue).Nullable()
                .WithAuditColumns();

            Create.UniqueConstraint(UniqueAttendanceConstraintName)
                .OnTable(DatabaseConfig.TableAttendance).WithSchema(DatabaseConfig.Schema_School)
                .Columns("classid", "studentid", "attendancedate");
        }

        if (!Schema.Schema(DatabaseConfig.Schema_School).Table(DatabaseConfig.TableAttendance).Index(ClassDateIndexName).Exists())
        {
            Create.Index(ClassDateIndexName)
                .OnTable(DatabaseConfig.TableAttendance).InSchema(DatabaseConfig.Schema_School)
                .OnColumn("classid").Ascending()
                .OnColumn("attendancedate").Ascending();
        }

        if (!Schema.Schema(DatabaseConfig.Schema_School).Table(DatabaseConfig.TableAttendance).Index(StudentIndexName).Exists())
        {
            Create.Index(StudentIndexName)
                .OnTable(DatabaseConfig.TableAttendance).InSchema(DatabaseConfig.Schema_School)
                .OnColumn("studentid").Ascending();
        }
    }

    public override void Down()
    {
        if (Schema.Schema(DatabaseConfig.Schema_School).Table(DatabaseConfig.TableAttendance).Exists())
        {
            Delete.Table(DatabaseConfig.TableAttendance).InSchema(DatabaseConfig.Schema_School);
        }
    }
}
