using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(29, "Global — seed timetable menus")]
public sealed class G029_SeedTimetableMenus : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid TimetableParentId = Guid.Parse("10000000-0000-0000-0000-000000000070");

    private static readonly (Guid Id, string Name, string Code, Guid? ParentId, string? Route, string Icon, int Order)[] Menus =
    [
        (TimetableParentId, "Timetable", MenuCodes.Timetable, null, null, "schedule", 26),
        (Guid.Parse("10000000-0000-0000-0000-000000000071"), "Period Templates", MenuCodes.PeriodMaster, TimetableParentId, "/timetable/periods", "view_day", 71),
        (Guid.Parse("10000000-0000-0000-0000-000000000072"), "Class Timetable", MenuCodes.ClassTimetable, TimetableParentId, "/timetable/grid", "calendar_view_week", 72),
        (Guid.Parse("10000000-0000-0000-0000-000000000073"), "My Timetable", MenuCodes.MyTimetable, TimetableParentId, "/timetable/my", "person", 73),
    ];

    public override void Up()
    {
        DateTimeOffset now = SchoolLocalTime.Now();

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

        string menuCodes = string.Join("','", Menus.Select(m => m.Code));
        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{SchoolLocalTime.Now():O}', '{SeedActor}', '{SchoolLocalTime.Now():O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.SmartOpsAdmin}')) AND m.code IN ('{menuCodes}')
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
