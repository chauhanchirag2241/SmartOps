using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(29, "Global — seed attendance report menu")]
public sealed class G029_SeedAttendanceReportMenu : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid MenuId = Guid.Parse("10000000-0000-0000-0000-000000000019");
    private static readonly Guid AcademicsParentId = Guid.Parse("10000000-0000-0000-0000-000000000010");

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{MenuId}', 'Attendance Report', '{MenuCodes.AttendanceReport}', '{AcademicsParentId}', '/attendance-report', 'analytics', 19, '{MenuApplications.School}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.AttendanceReport}'
);
""");

        string[] roleCodes =
        [
            RoleCodes.SchoolAdmin,
            RoleCodes.Hod,
            RoleCodes.Teacher
        ];

        foreach (string roleCode in roleCodes)
        {
            // View only for report
            bool canAdd = false;
            bool canEdit = false;
            bool canDelete = false;
            bool canExport = true; // Assuming they can export it

            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, {(canAdd ? "true" : "false")}, {(canEdit ? "true" : "false")}, {(canDelete ? "true" : "false")}, {(canExport ? "true" : "false")}, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE r.code = '{roleCode}' AND m.code = '{MenuCodes.AttendanceReport}'
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = m.id
  );
""");
        }

        // Admin role gets full permissions
        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE r.code = 'ADMIN' AND m.code = '{MenuCodes.AttendanceReport}'
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = m.id
  );
""");
    }

    public override void Down()
    {
        Execute.Sql($"""
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE menuid IN (SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.AttendanceReport}');
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.AttendanceReport}';
""");
    }
}
