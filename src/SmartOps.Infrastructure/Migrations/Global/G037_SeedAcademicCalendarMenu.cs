using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(37, "Global — seed academic calendar menu under academic setup")]
public sealed class G037_SeedAcademicCalendarMenu : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid MenuId = Guid.Parse("10000000-0000-0000-0000-000000000083");
    private static readonly Guid AcademicSetupParentId = Guid.Parse("10000000-0000-0000-0000-000000000045");

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
    (id, name, code, parentmenuid, route, icon, displayorder, application, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{MenuId}', 'Academic Calendar', '{MenuCodes.AcademicCalendar}', '{AcademicSetupParentId}', '/academic-calendar', 'calendar_month', 18, '{MenuApplications.School}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.AcademicCalendar}'
);
""");

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.SmartOpsAdmin}')) AND m.code = '{MenuCodes.AcademicCalendar}'
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
WHERE menuid IN (SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.AcademicCalendar}');
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.AcademicCalendar}';
""");
    }
}
