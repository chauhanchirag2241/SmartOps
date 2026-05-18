using System.Linq;
using FluentMigrator;
using SmartOps.Shared.Configuration;
using SmartOps.Shared.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Migration(15, "Global — seed school persona roles and menu permissions")]
public sealed class G015_SeedSchoolRolesAndPermissions : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private static readonly (Guid Id, string Name, string Code, string Description)[] Roles =
    [
        (Guid.Parse("20000000-0000-0000-0000-000000000002"), "School Admin", RoleCodes.SchoolAdmin, "School-level administrator"),
        (Guid.Parse("20000000-0000-0000-0000-000000000003"), "HOD", RoleCodes.Hod, "Head of department"),
        (Guid.Parse("20000000-0000-0000-0000-000000000004"), "Teacher", RoleCodes.Teacher, "Class teacher / subject teacher"),
        (Guid.Parse("20000000-0000-0000-0000-000000000005"), "Student", RoleCodes.Student, "Student portal user"),
        (Guid.Parse("20000000-0000-0000-0000-000000000006"), "Parent", RoleCodes.Parent, "Parent portal user"),
        (Guid.Parse("20000000-0000-0000-0000-000000000007"), "Accountant", RoleCodes.Accountant, "Accounts and fees user"),
        (Guid.Parse("20000000-0000-0000-0000-000000000008"), "Staff", RoleCodes.Staff, "General staff with custom scope"),
    ];

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((Guid id, string name, string code, string description) in Roles)
        {
            // Align code/description when role already exists by name (e.g. UI-created "Teacher").
            Execute.Sql($"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
SET code = '{code}',
    description = '{description}',
    isactive = true,
    updatedby = '{SeedActor}',
    updatedon = '{now:O}',
    versionno = versionno + 1
WHERE lower(trim(name)) = lower(trim('{name}'))
  AND code IS DISTINCT FROM '{code}';
""");

            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
    (id, name, code, description, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name}', '{code}', '{description}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
    WHERE code = '{code}' OR lower(trim(name)) = lower(trim('{name}'))
);
""");
        }

        SeedRoleMenuPermissions(RoleCodes.SchoolAdmin, view: true, add: true, edit: true, delete: true, export: true,
            menus: MenuCodes.All.Where(m => m != MenuCodes.Schools && m != MenuCodes.Users && m != MenuCodes.Roles).ToArray());

        SeedRoleMenuPermissions(RoleCodes.Hod,
            menus: [MenuCodes.Dashboard, MenuCodes.Students, MenuCodes.Teachers, MenuCodes.Classes, MenuCodes.Subjects, MenuCodes.Attendance, MenuCodes.AcademicYears],
            view: true, add: false, edit: false, delete: false, export: true);

        SeedRoleMenuPermissions(RoleCodes.Teacher,
            menus: [MenuCodes.Dashboard, MenuCodes.Students, MenuCodes.Classes, MenuCodes.Subjects, MenuCodes.Attendance],
            view: true, add: true, edit: true, delete: false, export: false,
            attendanceAdd: true);

        SeedRoleMenuPermissions(RoleCodes.Student,
            menus: [MenuCodes.Dashboard],
            view: true, add: false, edit: false, delete: false, export: false);

        SeedRoleMenuPermissions(RoleCodes.Parent,
            menus: [MenuCodes.Dashboard, MenuCodes.Students, MenuCodes.Attendance],
            view: true, add: false, edit: false, delete: false, export: false);

        SeedRoleMenuPermissions(RoleCodes.Accountant,
            menus: [MenuCodes.Dashboard],
            view: true, add: false, edit: false, delete: false, export: false);

        SeedRoleMenuPermissions(RoleCodes.Staff,
            menus: [MenuCodes.Dashboard],
            view: true, add: false, edit: false, delete: false, export: false);
    }

    private void SeedRoleMenuPermissions(
        string roleCode,
        string[] menus,
        bool view,
        bool add,
        bool edit,
        bool delete,
        bool export,
        bool attendanceAdd = false)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string menuList = string.Join("','", menus);

        bool canAdd = add;
        bool canEdit = edit;

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id,
    {(view ? "true" : "false")},
  CASE WHEN m.code = '{MenuCodes.Attendance}' AND '{roleCode}' = '{RoleCodes.Teacher}' THEN {(attendanceAdd ? "true" : "false")} ELSE {(canAdd ? "true" : "false")} END,
    {(canEdit ? "true" : "false")},
    {(delete ? "true" : "false")},
    {(export ? "true" : "false")},
    true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE r.code = '{roleCode}'
  AND m.code IN ('{menuList}')
  AND m.isactive = true
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = m.id
  );
""");
    }

    public override void Down()
    {
        string codes = string.Join("','", Roles.Select(r => r.Code));
        Execute.Sql($"""
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE roleid IN (SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} WHERE code IN ('{codes}'));
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} WHERE code IN ('{codes}');
""");
    }
}
