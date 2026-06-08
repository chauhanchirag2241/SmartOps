using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Migration(26, "Global — seed users roles settings menus for school portal")]
public sealed class G026_SeedSchoolPortalAdminMenus : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly (Guid Id, string Name, string Code, string Route, string Icon, int Order)[] Menus =
    [
        (Guid.Parse("10000000-0000-0000-0000-000000000030"), "Users", MenuCodes.Users, "/configuration/users", "group", 30),
        (Guid.Parse("10000000-0000-0000-0000-000000000031"), "Roles", MenuCodes.Roles, "/configuration/roles", "admin_panel_settings", 31),
        (Guid.Parse("10000000-0000-0000-0000-000000000032"), "Settings", MenuCodes.Settings, "/settings", "settings", 32),
    ];

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((Guid id, string name, string code, string route, string icon, int order) in Menus)
        {
            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name}', '{code}', NULL, '{route}', '{icon}', {order}, '{MenuApplications.School}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    WHERE code = '{code}' AND application = '{MenuApplications.School}'
);
""");
        }

        SeedRole(RoleCodes.SchoolAdmin, view: true, add: true, edit: true, delete: true);
        SeedRole(RoleCodes.Admin, view: true, add: true, edit: true, delete: true);
    }

    private void SeedRole(string roleCode, bool view, bool add, bool edit, bool delete)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string menuCodes = string.Join("','", Menus.Select(m => m.Code));

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, {(view ? "true" : "false")}, {(add ? "true" : "false")}, {(edit ? "true" : "false")}, {(delete ? "true" : "false")}, false, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE r.code = '{roleCode}' AND m.code IN ('{menuCodes}') AND m.application = '{MenuApplications.School}'
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
WHERE menuid IN (
    SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    WHERE code IN ('{codes}') AND application = '{MenuApplications.School}'
);
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
WHERE code IN ('{codes}') AND application = '{MenuApplications.School}';
""");
    }
}
