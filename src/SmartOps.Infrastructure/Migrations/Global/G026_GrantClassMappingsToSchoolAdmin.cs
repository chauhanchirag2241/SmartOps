using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.Migrations.Global;

/// <summary>
/// Ensures TEACHERS is granted to platform Admin (idempotent with G014).
/// </summary>
[Tags("Global")]
[Migration(26, "Global — grant Teachers menu to admin")]
public sealed class G026_GrantClassMappingsToSchoolAdmin : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    public override void Up()
    {
        DateTimeOffset now = SchoolLocalTime.Now();
        string g = DatabaseConfig.Schema_Global;

        Execute.Sql($"""
INSERT INTO {g}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {g}.{DatabaseConfig.TableRoles} r
CROSS JOIN {g}.{DatabaseConfig.TableMenus} m
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.SmartOpsAdmin}')) AND m.code = '{MenuCodes.Teachers}'
  AND NOT EXISTS (
    SELECT 1 FROM {g}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = m.id
  );
""");
    }

    public override void Down()
    {
        string g = DatabaseConfig.Schema_Global;

        Execute.Sql($"""
DELETE FROM {g}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE roleid IN (SELECT id FROM {g}.{DatabaseConfig.TableRoles} WHERE lower(trim(name)) = lower(trim('{RoleNames.SmartOpsAdmin}')))
  AND menuid IN (SELECT id FROM {g}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.Teachers}');
""");
    }
}
