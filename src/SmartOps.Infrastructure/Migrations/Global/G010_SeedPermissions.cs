using FluentMigrator;
using SmartOps.Shared.Configuration;
using SmartOps.Shared.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Migration(10, "Global — seed permissions")]
public sealed class G010_SeedPermissions : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        string[] permissionNames =
        [
            PermissionNames.StudentRead,
            PermissionNames.StudentCreate,
            PermissionNames.StudentUpdate,
            PermissionNames.StudentDelete,
            PermissionNames.AttendanceRead,
            PermissionNames.AttendanceMark,
            PermissionNames.FeesRead,
            PermissionNames.FeesCreate,
            PermissionNames.FeesUpdate,
            PermissionNames.ExamsRead,
            PermissionNames.ExamsCreate,
            PermissionNames.HrRead,
            PermissionNames.HrManage,
            PermissionNames.ReportsView,
            PermissionNames.AdminFull,
            PermissionNames.TeacherRead,
            PermissionNames.ClassRead,
            PermissionNames.SubjectRead,
            PermissionNames.AcademicYearRead,
            PermissionNames.RolesManage,
            PermissionNames.SettingsRead
        ];

        foreach (string name in permissionNames)
        {
            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TablePermissions}
    (id, name, description, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), '{name}', NULL, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TablePermissions} WHERE name = '{name}'
);
""");
        }
    }

    public override void Down()
    {
        Execute.Sql(
            $"""
            DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TablePermissions}
            WHERE name IN (
                'student.read','student.create','student.update','student.delete',
                'attendance.read','attendance.mark',
                'fees.read','fees.create','fees.update',
                'exams.read','exams.create',
                'hr.read','hr.manage',
                'reports.view','admin.full',
                'teacher.read','class.read','subject.read','academicyear.read',
                'roles.manage','settings.read'
            );
            """);
    }
}
