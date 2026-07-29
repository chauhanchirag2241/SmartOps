using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(11, "Global — seed admin role and permissions")]
public sealed class G011_SeedRolesAndRolePermissions : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid AdminRoleId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
    (id, name, description, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{AdminRoleId}', '{RoleNames.Admin}', 'Default administrator role', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
    WHERE lower(trim(name)) = lower(trim('{RoleNames.Admin}'))
);
""");

        Execute.Sql($"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles} r
CROSS JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.Admin}')) AND m.isactive = true
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
WHERE roleid IN (
    SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
    WHERE lower(trim(name)) = lower(trim('{RoleNames.Admin}'))
);
DELETE FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
WHERE lower(trim(name)) = lower(trim('{RoleNames.Admin}'));
""");
    }
}
