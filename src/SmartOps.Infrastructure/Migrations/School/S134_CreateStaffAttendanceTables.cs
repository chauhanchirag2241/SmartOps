using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Migrations.Extensions;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(134, "School template — staff attendance + face enrollments + employee photourl")]
public sealed class S134_CreateStaffAttendanceTables : Migration
{
    private static string S => DatabaseConfig.Schema_School;
    private const string UniqueStaffAttendance = "uq_staffattendance_employee_date";
    private const string UniqueActiveFaceEnrollment = "uq_employeefaceenrollments_employee_active";
    private const string IxStaffAttendanceDate = "ix_staffattendance_date";
    private const string IxStaffAttendanceEmployee = "ix_staffattendance_employee";

    public override void Up()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Exists()
            && !Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Column("photourl").Exists())
        {
            Alter.Table(DatabaseConfig.TableEmployees).InSchema(S)
                .AddColumn("photourl").AsString(int.MaxValue).Nullable();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableStaffAttendance).Exists())
        {
            Create.Table(DatabaseConfig.TableStaffAttendance).InSchema(S)
                .WithColumn("id").AsGuid().PrimaryKey().NotNullable().WithDefaultValue(RawSql.Insert("gen_random_uuid()"))
                .WithColumn("employeeid").AsGuid().NotNullable()
                .WithColumn("attendancedate").AsDate().NotNullable()
                .WithColumn("checkintime").AsDateTimeOffset().Nullable()
                .WithColumn("checkouttime").AsDateTimeOffset().Nullable()
                .WithColumn("checkinsource").AsString(20).Nullable()
                .WithColumn("checkoutsource").AsString(20).Nullable()
                .WithColumn("status").AsInt16().NotNullable().WithDefaultValue(1)
                .WithColumn("remarks").AsString(int.MaxValue).Nullable()
                .WithColumn("checkinconfidence").AsFloat().Nullable()
                .WithColumn("checkoutconfidence").AsFloat().Nullable()
                .WithColumn("markedbyuserid").AsGuid().NotNullable()
                .WithAuditColumns();

            Create.UniqueConstraint(UniqueStaffAttendance)
                .OnTable(DatabaseConfig.TableStaffAttendance).WithSchema(S)
                .Columns("employeeid", "attendancedate");

            Create.Index(IxStaffAttendanceDate)
                .OnTable(DatabaseConfig.TableStaffAttendance).InSchema(S)
                .OnColumn("attendancedate").Ascending();

            Create.Index(IxStaffAttendanceEmployee)
                .OnTable(DatabaseConfig.TableStaffAttendance).InSchema(S)
                .OnColumn("employeeid").Ascending();
        }

        if (!Schema.Schema(S).Table(DatabaseConfig.TableEmployeeFaceEnrollments).Exists())
        {
            Execute.Sql($"""
CREATE TABLE {S}.{DatabaseConfig.TableEmployeeFaceEnrollments} (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    employeeid uuid NOT NULL,
    embedding real[] NOT NULL,
    photourl text NULL,
    modelname varchar(50) NOT NULL DEFAULT 'buffalo_l',
    isactive boolean NOT NULL DEFAULT true,
    versionno integer NOT NULL DEFAULT 1,
    createdby uuid NOT NULL DEFAULT '{DatabaseConfig.SystemUserId}',
    createdon timestamptz NOT NULL DEFAULT NOW(),
    updatedby uuid NOT NULL DEFAULT '{DatabaseConfig.SystemUserId}',
    updatedon timestamptz NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX {UniqueActiveFaceEnrollment}
    ON {S}.{DatabaseConfig.TableEmployeeFaceEnrollments} (employeeid)
    WHERE isactive = true;
""");
        }
    }

    public override void Down()
    {
        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployeeFaceEnrollments).Exists())
        {
            Delete.Table(DatabaseConfig.TableEmployeeFaceEnrollments).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableStaffAttendance).Exists())
        {
            Delete.Table(DatabaseConfig.TableStaffAttendance).InSchema(S);
        }

        if (Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Exists()
            && Schema.Schema(S).Table(DatabaseConfig.TableEmployees).Column("photourl").Exists())
        {
            Delete.Column("photourl").FromTable(DatabaseConfig.TableEmployees).InSchema(S);
        }
    }
}
