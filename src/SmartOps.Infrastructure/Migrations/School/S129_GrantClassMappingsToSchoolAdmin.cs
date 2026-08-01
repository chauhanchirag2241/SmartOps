using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.School;

/// <summary>
/// Grants TEACHERS menu permission to Admin on school DB (menus live on platform global only).
/// </summary>
[Tags("School")]
[Migration(129, "School database — grant Teachers menu to admin")]
public sealed class S129_GrantClassMappingsToSchoolAdmin : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    /// <summary>Fixed menu id from <c>G014_SeedClassMappingsMenu</c>.</summary>
    private static readonly Guid TeachersMenuId = Guid.Parse("10000000-0000-0000-0000-000000000017");

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string man = DatabaseConfig.Schema_Man;

        Execute.Sql($"""
INSERT INTO {man}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, '{TeachersMenuId}', true, true, true, true, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {man}.{DatabaseConfig.TableRoles} r
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.SchoolAdmin}'))
  AND NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = '{TeachersMenuId}'
  );
""");
    }

    public override void Down()
    {
        string man = DatabaseConfig.Schema_Man;

        Execute.Sql($"""
DELETE FROM {man}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE roleid IN (SELECT id FROM {man}.{DatabaseConfig.TableRoles} WHERE lower(trim(name)) = lower(trim('{RoleNames.SchoolAdmin}')))
  AND menuid = '{TeachersMenuId}';
""");
    }
}
