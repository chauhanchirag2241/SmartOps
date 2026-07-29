using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(28, "Global — seed exam management menus")]
public sealed class G028_SeedExamMenus : Migration
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

        // Platform admin: everything.
        string menuCodes = string.Join("','", Menus.Select(m => m.Code));
        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{DateTimeOffset.UtcNow:O}', '{SeedActor}', '{DateTimeOffset.UtcNow:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.Admin}')) AND m.code IN ('{menuCodes}')
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
