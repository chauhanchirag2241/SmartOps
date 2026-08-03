using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(31, "Global — seed staff attendance menus + attendance.employee.type defaults")]
public sealed class G031_SeedStaffAttendanceMenusAndSettings : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid StaffAttendanceMenuId = Guid.Parse("10000000-0000-0000-0000-000000000075");
    private static readonly Guid StaffAttendanceReportMenuId = Guid.Parse("10000000-0000-0000-0000-000000000076");
    private static readonly Guid AdministrationParentId = Guid.Parse("10000000-0000-0000-0000-000000000043");
    private static readonly Guid ReportsParentId = Guid.Parse("10000000-0000-0000-0000-000000000044");

    public override void Up()
    {
        DateTimeOffset now = SchoolLocalTime.Now();

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{StaffAttendanceMenuId}', 'Staff Attendance', '{MenuCodes.StaffAttendance}', '{AdministrationParentId}', '/staff-attendance', 'fingerprint', 51, '{MenuApplications.School}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.StaffAttendance}'
);

INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{StaffAttendanceReportMenuId}', 'Staff Attendance Report', '{MenuCodes.StaffAttendanceReport}', '{ReportsParentId}', '/staff-attendance-report', 'assessment', 63, '{MenuApplications.School}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.StaffAttendanceReport}'
);
""");

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{SchoolLocalTime.Now():O}', '{SeedActor}', '{SchoolLocalTime.Now():O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.SmartOpsAdmin}')) AND m.code IN ('{MenuCodes.StaffAttendance}', '{MenuCodes.StaffAttendanceReport}')
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = m.id
  );
""");

        // schoolsettings SoT is school man — do not seed platform schoolsettings.
    }

    public override void Down()
    {
        Execute.Sql($"""
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE menuid IN (
    SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    WHERE code IN ('{MenuCodes.StaffAttendance}', '{MenuCodes.StaffAttendanceReport}')
);
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
WHERE code IN ('{MenuCodes.StaffAttendance}', '{MenuCodes.StaffAttendanceReport}');
""");
    }
}
