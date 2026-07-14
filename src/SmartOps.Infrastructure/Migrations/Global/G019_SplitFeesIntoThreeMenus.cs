using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(19, "Global — ensure fee menus sit under Fee group")]
public sealed class G019_SplitFeesIntoThreeMenus : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid FeesManagementParentId = Guid.Parse("10000000-0000-0000-0000-000000000040");

    private static readonly (Guid Id, string Name, string Code, string Route, string Icon, int Order)[] Menus =
    [
        (Guid.Parse("10000000-0000-0000-0000-000000000020"), "Fee Structure", MenuCodes.FeesStructure, "/fees-structure", "list_alt", 21),
        (Guid.Parse("10000000-0000-0000-0000-000000000021"), "Class-wise Amounts", MenuCodes.FeesClassAmounts, "/fees-class-amounts", "school", 22),
        (Guid.Parse("10000000-0000-0000-0000-000000000022"), "Fee Collection", MenuCodes.FeesCollection, "/fees-collection", "payments", 23),
    ];

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Execute.Sql($"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
SET isactive = false, updatedby = '{SeedActor}', updatedon = '{now:O}', versionno = versionno + 1
WHERE code = 'FEES';
""");

        foreach ((Guid id, string name, string code, string route, string icon, int order) in Menus)
        {
            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name}', '{code}', '{FeesManagementParentId}', '{route}', '{icon}', {order}, '{MenuApplications.School}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{code}'
);
""");

            // Reparent + rename if menus were seeded earlier under Academics / as Student Collection
            Execute.Sql($"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
SET name = '{name}',
    parentmenuid = '{FeesManagementParentId}',
    route = '{route}',
    icon = '{icon}',
    displayorder = {order},
    isactive = true,
    updatedby = '{SeedActor}',
    updatedon = '{now:O}',
    versionno = versionno + 1
WHERE code = '{code}';
""");
        }

        string[] roleCodes = [RoleCodes.SchoolAdmin, RoleCodes.Accountant];
        string menuCodes = string.Join("','", Menus.Select(m => m.Code));

        foreach (string roleCode in roleCodes)
        {
            bool canAdd = true;
            bool canEdit = true;
            bool canDelete = roleCode == RoleCodes.SchoolAdmin;

            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, {(canAdd ? "true" : "false")}, {(canEdit ? "true" : "false")}, {(canDelete ? "true" : "false")}, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE r.code = '{roleCode}' AND m.code IN ('{menuCodes}')
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = m.id
  );
""");
        }

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

    public override void Down()
    {
        string codes = string.Join("','", Menus.Select(m => m.Code));
        Execute.Sql($"""
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE menuid IN (SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code IN ('{codes}'));
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code IN ('{codes}');
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} SET isactive = true WHERE code = 'FEES';
""");
    }
}
