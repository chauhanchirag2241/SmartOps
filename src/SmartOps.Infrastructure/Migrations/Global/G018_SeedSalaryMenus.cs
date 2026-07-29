using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(18, "Global — seed salary module menus")]
public sealed class G018_SeedSalaryMenus : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid SalaryManagementParentId = Guid.Parse("10000000-0000-0000-0000-000000000041");

    private static readonly (Guid Id, string Name, string Code, string Route, string Icon, int Order)[] Menus =
    [
        (Guid.Parse("10000000-0000-0000-0000-000000000023"), "Salary Structure", MenuCodes.SalaryStructure, "/salary-structure", "list_alt", 31),
        (Guid.Parse("10000000-0000-0000-0000-000000000024"), "Employee Salary", MenuCodes.SalaryEmployees, "/salary-employees", "groups", 32),
        (Guid.Parse("10000000-0000-0000-0000-000000000025"), "Payroll", MenuCodes.SalaryPayroll, "/salary-payroll", "payments", 33),
    ];

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((Guid id, string name, string code, string route, string icon, int order) in Menus)
        {
            Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name}', '{code}', '{SalaryManagementParentId}', '{route}', '{icon}', {order}, '{MenuApplications.School}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{code}'
);
""");
        }

        string menuCodes = string.Join("','", Menus.Select(m => m.Code));

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
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
