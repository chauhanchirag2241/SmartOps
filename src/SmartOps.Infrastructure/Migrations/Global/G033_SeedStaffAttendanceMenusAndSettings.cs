using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(33, "Global — seed staff attendance menus + attendance.employee.type defaults")]
public sealed class G033_SeedStaffAttendanceMenusAndSettings : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid StaffAttendanceMenuId = Guid.Parse("10000000-0000-0000-0000-000000000075");
    private static readonly Guid StaffAttendanceReportMenuId = Guid.Parse("10000000-0000-0000-0000-000000000076");
    private static readonly Guid AdministrationParentId = Guid.Parse("10000000-0000-0000-0000-000000000043");
    private static readonly Guid ReportsParentId = Guid.Parse("10000000-0000-0000-0000-000000000044");

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

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

        // Mark screen: SchoolAdmin, HOD, Teacher (self punch + mark where permitted)
        foreach (string roleCode in new[] { RoleCodes.SchoolAdmin, RoleCodes.Hod, RoleCodes.Teacher })
        {
            InsertFullPerm(roleCode, MenuCodes.StaffAttendance);
        }

        foreach (string roleCode in new[] { RoleCodes.SchoolAdmin, RoleCodes.Hod })
        {
            InsertViewExportPerm(roleCode, MenuCodes.StaffAttendanceReport);
        }

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{DateTimeOffset.UtcNow:O}', '{SeedActor}', '{DateTimeOffset.UtcNow:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE r.code = 'ADMIN' AND m.code IN ('{MenuCodes.StaffAttendance}', '{MenuCodes.StaffAttendanceReport}')
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = m.id
  );

INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchoolSettings}
    (id, schoolid, settingkey, settingvalue, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), s.id, '{EmployeeAttendanceSettingKeys.EmployeeType}', '{EmployeeAttendanceTypes.Both}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools} s
WHERE s.isactive = true
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchoolSettings} ss
    WHERE ss.schoolid = s.id AND ss.settingkey = '{EmployeeAttendanceSettingKeys.EmployeeType}' AND ss.isactive = true
  );
""");
    }

    private void InsertFullPerm(string roleCode, string menuCode)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, false, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE r.code = '{roleCode}' AND m.code = '{menuCode}'
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = m.id
  );
""");
    }

    private void InsertViewExportPerm(string roleCode, string menuCode)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, false, false, false, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE r.code = '{roleCode}' AND m.code = '{menuCode}'
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
WHERE menuid IN (
    SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    WHERE code IN ('{MenuCodes.StaffAttendance}', '{MenuCodes.StaffAttendanceReport}')
);
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
WHERE code IN ('{MenuCodes.StaffAttendance}', '{MenuCodes.StaffAttendanceReport}');
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchoolSettings}
WHERE settingkey = '{EmployeeAttendanceSettingKeys.EmployeeType}';
""");
    }
}
