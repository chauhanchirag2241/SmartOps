using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(30, "Global — seed exam management menus")]
public sealed class G030_SeedExamMenus : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid ExamManagementParentId = Guid.Parse("10000000-0000-0000-0000-000000000060");

    private static readonly (Guid Id, string Name, string Code, Guid? ParentId, string? Route, string Icon, int Order)[] Menus =
    [
        (ExamManagementParentId, "Exam Management", MenuCodes.ExamManagement, null, null, "history_edu", 25),
        (Guid.Parse("10000000-0000-0000-0000-000000000061"), "Exam Groups", MenuCodes.ExamGroups, ExamManagementParentId, "/exams/groups", "folder_special", 61),
        (Guid.Parse("10000000-0000-0000-0000-000000000062"), "Exams", MenuCodes.Exams, ExamManagementParentId, "/exams/list", "event_note", 62),
        (Guid.Parse("10000000-0000-0000-0000-000000000063"), "Exam Schedule", MenuCodes.ExamSchedule, ExamManagementParentId, "/exams/schedule", "calendar_month", 63),
        (Guid.Parse("10000000-0000-0000-0000-000000000064"), "Marks Entry", MenuCodes.ExamMarksEntry, ExamManagementParentId, "/exams/marks-entry", "edit_note", 64),
        (Guid.Parse("10000000-0000-0000-0000-000000000065"), "Results", MenuCodes.ExamResults, ExamManagementParentId, "/exams/results", "bar_chart", 65),
        (Guid.Parse("10000000-0000-0000-0000-000000000066"), "Hall Tickets", MenuCodes.ExamHallTickets, ExamManagementParentId, "/exams/hall-tickets", "confirmation_number", 66),
        (Guid.Parse("10000000-0000-0000-0000-000000000067"), "Grade Setup", MenuCodes.ExamGradeSetup, ExamManagementParentId, "/exams/grade-setup", "grade", 67),
    ];

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((Guid id, string name, string code, Guid? parentId, string? route, string icon, int order) in Menus)
        {
            string parentSql = parentId.HasValue ? $"'{parentId}'" : "NULL";
            string routeSql = route is null ? "NULL" : $"'{route}'";
            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name}', '{code}', {parentSql}, {routeSql}, '{icon}', {order}, '{MenuApplications.School}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{code}'
);
""");
        }

        // School admin: full control over the whole exam module.
        foreach ((_, _, string code, _, _, _, _) in Menus)
        {
            InsertPerm(RoleCodes.SchoolAdmin, code, view: true, add: true, edit: true, delete: true, export: true);
        }

        // Teachers: browse the module, enter/update marks, view schedules & results.
        string[] teacherRoles = [RoleCodes.Teacher, RoleCodes.Hod];
        foreach (string role in teacherRoles)
        {
            InsertPerm(role, MenuCodes.ExamManagement, view: true, add: false, edit: false, delete: false, export: false);
            InsertPerm(role, MenuCodes.ExamGroups, view: true, add: false, edit: false, delete: false, export: false);
            InsertPerm(role, MenuCodes.Exams, view: true, add: false, edit: false, delete: false, export: false);
            InsertPerm(role, MenuCodes.ExamSchedule, view: true, add: false, edit: false, delete: false, export: true);
            InsertPerm(role, MenuCodes.ExamMarksEntry, view: true, add: true, edit: true, delete: false, export: false);
            InsertPerm(role, MenuCodes.ExamResults, view: true, add: false, edit: false, delete: false, export: true);
            InsertPerm(role, MenuCodes.ExamHallTickets, view: true, add: false, edit: false, delete: false, export: true);
        }

        // Platform admin: everything.
        string menuCodes = string.Join("','", Menus.Select(m => m.Code));
        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{DateTimeOffset.UtcNow:O}', '{SeedActor}', '{DateTimeOffset.UtcNow:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE r.code = 'ADMIN' AND m.code IN ('{menuCodes}')
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = m.id
  );
""");
    }

    private void InsertPerm(string roleCode, string menuCode, bool view, bool add, bool edit, bool delete, bool export)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, {(view ? "true" : "false")}, {(add ? "true" : "false")}, {(edit ? "true" : "false")}, {(delete ? "true" : "false")}, {(export ? "true" : "false")}, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
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
