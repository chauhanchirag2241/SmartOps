using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.School;

/// <summary>
/// Dedicated school databases store menus/permissions in <c>global</c> — sync CLASS_MAPPINGS for School Admin.
/// </summary>
[Tags("School")]
[Migration(129, "School database — grant class mapping menu to school admin")]
public sealed class S129_GrantClassMappingsToSchoolAdmin : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    public override void Up()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string g = DatabaseConfig.Schema_Global;

        Execute.Sql($"""
INSERT INTO {g}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, m.id, true, true, true, true, true, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {g}.{DatabaseConfig.TableRoles} r
CROSS JOIN {g}.{DatabaseConfig.TableMenus} m
WHERE r.code = '{RoleCodes.SchoolAdmin}' AND m.code = '{MenuCodes.ClassMappings}'
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
WHERE roleid IN (SELECT id FROM {g}.{DatabaseConfig.TableRoles} WHERE code = '{RoleCodes.SchoolAdmin}')
  AND menuid IN (SELECT id FROM {g}.{DatabaseConfig.TableMenus} WHERE code = '{MenuCodes.ClassMappings}');
""");
    }
}
