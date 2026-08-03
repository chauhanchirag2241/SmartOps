using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(151, "School — grant leave balance menus + yearly CF setting")]
public sealed class S151_GrantLeaveBalanceMenusAndSettings : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid LeaveTypesMenuId = Guid.Parse("10000000-0000-0000-0000-000000000084");
    private static readonly Guid LeavePoliciesMenuId = Guid.Parse("10000000-0000-0000-0000-000000000085");
    private static readonly Guid LeaveBalancesMenuId = Guid.Parse("10000000-0000-0000-0000-000000000086");

    public override void Up()
    {
        DateTimeOffset now = SchoolLocalTime.Now();
        string man = DatabaseConfig.Schema_Man;

        foreach (Guid menuId in new[] { LeaveTypesMenuId, LeavePoliciesMenuId, LeaveBalancesMenuId })
        {
            Execute.Sql($"""
INSERT INTO {man}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, '{menuId}', true, true, true, true, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {man}.{DatabaseConfig.TableRoles} r
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.SchoolAdmin}'))
  AND NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = '{menuId}'
  );
""");
        }

        // Copy schoolid from any existing leave setting row
        Execute.Sql($"""
INSERT INTO {man}.{DatabaseConfig.TableSchoolSettings}
    (id, schoolid, settingkey, settingvalue, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), src.schoolid, '{LeaveSettingKeys.YearlyCarryForwardDays}', '15', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM (
    SELECT DISTINCT schoolid
    FROM {man}.{DatabaseConfig.TableSchoolSettings}
    WHERE isactive = true
) src
WHERE NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableSchoolSettings} ss
    WHERE ss.schoolid = src.schoolid AND ss.settingkey = '{LeaveSettingKeys.YearlyCarryForwardDays}'
);
""");
    }

    public override void Down()
    {
    }
}
