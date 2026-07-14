using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(23, "Global — seed leave and actions menus")]
public sealed class G023_SeedLeaveAndActionsMenus : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid LeaveManagementParentId = Guid.Parse("10000000-0000-0000-0000-000000000042");
    private static readonly Guid AdministrationParentId = Guid.Parse("10000000-0000-0000-0000-000000000043");

    private static readonly (Guid Id, string Name, string Code, Guid? ParentId, string Route, string Icon, int Order)[] Menus =
    [
        (Guid.Parse("10000000-0000-0000-0000-000000000026"), "Staff Leave", MenuCodes.LeaveStaff, LeaveManagementParentId, "/leave/staff", "event_busy", 41),
        (Guid.Parse("10000000-0000-0000-0000-000000000027"), "Student Leave", MenuCodes.LeaveStudent, LeaveManagementParentId, "/leave/students", "child_care", 42),
        // Root-level like Dashboard (directly after Dashboard in display order)
        (Guid.Parse("10000000-0000-0000-0000-000000000028"), "My Actions", MenuCodes.MyActions, null, "/my-actions", "pending_actions", 2),
        (Guid.Parse("10000000-0000-0000-0000-000000000029"), "Notices", MenuCodes.Notices, AdministrationParentId, "/notices", "campaign", 53),
    ];

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((Guid id, string name, string code, Guid? parentId, string route, string icon, int order) in Menus)
        {
            string parentSql = parentId.HasValue ? $"'{parentId}'" : "NULL";
            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name}', '{code}', {parentSql}, '{route}', '{icon}', {order}, '{MenuApplications.School}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{code}'
);
""");
        }

        SeedRole(RoleCodes.Teacher, leaveStaffView: true, leaveStaffAdd: true, leaveStudentView: false, leaveStudentAdd: false, myActions: true, notices: false);
        SeedRole(RoleCodes.Hod, leaveStaffView: true, leaveStaffAdd: true, leaveStudentView: true, leaveStudentAdd: false, myActions: true, notices: true);
        SeedRole(RoleCodes.SchoolAdmin, leaveStaffView: true, leaveStaffAdd: true, leaveStudentView: true, leaveStudentAdd: false, myActions: true, notices: true);
        SeedRole(RoleCodes.Parent, leaveStaffView: false, leaveStaffAdd: false, leaveStudentView: true, leaveStudentAdd: true, myActions: true, notices: false);

        string menuCodes = string.Join("','", Menus.Select(m => m.Code));
        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE r.code = 'ADMIN' AND m.code IN ('{menuCodes}')
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = m.id
  );
""");
    }

    private void SeedRole(
        string roleCode,
        bool leaveStaffView,
        bool leaveStaffAdd,
        bool leaveStudentView,
        bool leaveStudentAdd,
        bool myActions,
        bool notices)
    {
        InsertPerm(roleCode, MenuCodes.LeaveStaff, leaveStaffView, leaveStaffAdd, leaveStaffAdd, false);
        InsertPerm(roleCode, MenuCodes.LeaveStudent, leaveStudentView, leaveStudentAdd, leaveStudentAdd, false);
        InsertPerm(roleCode, MenuCodes.MyActions, myActions, myActions, myActions, false);
        InsertPerm(roleCode, MenuCodes.Notices, notices, notices, notices, notices);
    }

    private void InsertPerm(string roleCode, string menuCode, bool view, bool add, bool edit, bool delete)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, {(view ? "true" : "false")}, {(add ? "true" : "false")}, {(edit ? "true" : "false")}, {(delete ? "true" : "false")}, false, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
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
        string codes = string.Join("','", Menus.Select(m => m.Code));
        Execute.Sql($"""
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE menuid IN (SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code IN ('{codes}'));
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code IN ('{codes}');
""");
    }
}
