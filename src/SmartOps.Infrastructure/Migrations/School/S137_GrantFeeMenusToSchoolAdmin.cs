using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(137, "School database — grant fee management menus to admin")]
public sealed class S137_GrantFeeMenusToSchoolAdmin : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private static readonly Guid[] FeeMenuIds =
    [
        Guid.Parse("10000000-0000-0000-0000-000000000080"), // Fee Management
        Guid.Parse("10000000-0000-0000-0000-000000000081"), // Fee Master
    ];

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string man = DatabaseConfig.Schema_Man;

        foreach (Guid menuId in FeeMenuIds)
        {
            Execute.Sql($"""
INSERT INTO {man}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, '{menuId}', true, true, true, true, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {man}.{DatabaseConfig.TableRoles} r
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.Admin}'))
  AND NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = '{menuId}'
  );
""");
        }
    }

    public override void Down()
    {
        string man = DatabaseConfig.Schema_Man;
        string menuIds = string.Join("','", FeeMenuIds);

        Execute.Sql($"""
DELETE FROM {man}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE roleid IN (SELECT id FROM {man}.{DatabaseConfig.TableRoles} WHERE lower(trim(name)) = lower(trim('{RoleNames.Admin}')))
  AND menuid IN ('{menuIds}');
""");
    }
}
