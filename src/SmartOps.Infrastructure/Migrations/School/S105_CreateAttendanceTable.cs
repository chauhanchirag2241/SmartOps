using FluentMigrator;
using SmartOps.Infrastructure.Migrations.Extensions;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(105, "School template — attendance")]
public sealed class S105_CreateAttendanceTable : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string UniqueAttendanceConstraintName = "uq_attendance_class_student_date";
    private const string ClassDateIndexName = "ix_attendance_classdate";
    private const string StudentIndexName = "ix_attendance_student";

    public override void Up()
    {
        if (!Schema.Schema(S).Table(DatabaseConfig.TableAttendance).Exists())
        {
            Create.Table(DatabaseConfig.TableAttendance).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("classid").AsGuid().NotNullable()
                .WithColumn("studentid").AsGuid().NotNullable()
                .WithColumn("teacherid").AsGuid().NotNullable()
                .WithColumn("attendancedate").AsDate().NotNullable()
                .WithColumn("status").AsInt16().NotNullable()
                .WithColumn("remarks").AsString(int.MaxValue).Nullable()
                .WithAuditColumns();
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableAttendance).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableAttendance).Constraint(UniqueAttendanceConstraintName).Exists())
        {
            Create.UniqueConstraint(UniqueAttendanceConstraintName)
                .OnTable(DatabaseConfig.TableAttendance).WithSchema(S)
                .Columns("classid", "studentid", "attendancedate");
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableAttendance).Index(ClassDateIndexName).Exists())
        {
            Create.Index(ClassDateIndexName)
                .OnTable(DatabaseConfig.TableAttendance).InSchema(S)
                .OnColumn("classid").Ascending()
                .OnColumn("attendancedate").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableAttendance).Index(StudentIndexName).Exists())
        {
            Create.Index(StudentIndexName)
                .OnTable(DatabaseConfig.TableAttendance).InSchema(S)
                .OnColumn("studentid").Ascending();
        }
    }

    public override void Down() => Delete.Table(DatabaseConfig.TableAttendance).InSchema(S);
}
