using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.Migrations.School;

[Tags("School")]
[Migration(144, "School database — grant roll numbers menu to admin")]
public sealed class S144_GrantRollNumbersMenuToSchoolAdmin : Migration
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);
    private static readonly Guid RollNumbersMenuId = Guid.Parse("10000000-0000-0000-0000-000000000049");

    public override void Up()
    {
        DateTimeOffset now = SchoolLocalTime.Now();
        string man = DatabaseConfig.Schema_Man;

        Execute.Sql($"""
INSERT INTO {man}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, '{RollNumbersMenuId}', true, false, true, false, false, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {man}.{DatabaseConfig.TableRoles} r
WHERE lower(trim(r.name)) = lower(trim('{RoleNames.SchoolAdmin}'))
  AND NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = '{RollNumbersMenuId}'
  );
""");
    }

    public override void Down()
    {
        string man = DatabaseConfig.Schema_Man;

        Execute.Sql($"""
DELETE FROM {man}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE roleid IN (SELECT id FROM {man}.{DatabaseConfig.TableRoles} WHERE lower(trim(name)) = lower(trim('{RoleNames.SchoolAdmin}')))
  AND menuid = '{RollNumbersMenuId}';
""");
    }
}
