using FluentMigrator;
using SmartOps.Shared.Configuration;
using SmartOps.Shared.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Migration(11, "Global — seed roles")]
public sealed class G011_SeedRolesAndRolePermissions : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private static readonly (string Role, string Description, string[] Permissions)[] RoleMatrix =
    [
        ("SchoolAdmin", "School principal / administrator", AllPermissions()),
        ("Teacher", "Academic staff", [
            PermissionNames.StudentRead,
            PermissionNames.AttendanceRead,
            PermissionNames.AttendanceMark,
            PermissionNames.TeacherRead,
            PermissionNames.ClassRead,
            PermissionNames.SubjectRead,
            PermissionNames.AcademicYearRead
        ]),
        ("Accountant", "Finance staff", [
            PermissionNames.FeesRead,
            PermissionNames.FeesCreate,
            PermissionNames.FeesUpdate,
            PermissionNames.ReportsView
        ]),
        ("Student", "Student portal", [PermissionNames.StudentRead]),
        ("Parent", "Parent portal", [
            PermissionNames.StudentRead,
            PermissionNames.AttendanceRead,
            PermissionNames.FeesRead
        ]),
        ("PlatformAdmin", "SmartOps platform operator", [PermissionNames.AdminFull])
    ];

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((string role, string description, string[] permissions) in RoleMatrix)
        {
            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
    (id, name, description, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), '{role}', '{description.Replace("'", "''")}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} WHERE name = '{role}'
);
""");

            foreach (string permission in permissions)
            {
                Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions}
    (roleid, permissionid, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT r.id, p.id, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TablePermissions} p
WHERE r.name = '{role}' AND p.name = '{permission}'
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions} rp
    WHERE rp.roleid = r.id AND rp.permissionid = p.id
  );
""");
            }
        }
    }

    public override void Down()
    {
        string[] roles = ["SchoolAdmin", "Teacher", "Accountant", "Student", "Parent", "PlatformAdmin"];
        string roleList = string.Join("','", roles);
        Execute.Sql($"""
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions}
WHERE roleid IN (SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} WHERE name IN ('{roleList}'));
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} WHERE name IN ('{roleList}');
""");
    }

    private static string[] AllPermissions() =>
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
}
