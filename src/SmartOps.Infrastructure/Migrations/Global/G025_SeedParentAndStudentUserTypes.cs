using FluentMigrator;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Migrations.Global;

[Tags("Global")]
[Migration(25, "Global — seed Parent and Student user types")]
public sealed class G025_SeedParentAndStudentUserTypes : Migration
{
    private const string UserTypesTable = "usertypes";
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    private static readonly (Guid Id, string Code, string Name)[] UserTypes =
    [
        (Guid.Parse("30000000-0000-0000-0000-000000000008"), UserTypeCodes.Student, "Student"),
        (Guid.Parse("30000000-0000-0000-0000-000000000009"), UserTypeCodes.Parent, "Parent"),
    ];

    public override void Up()
    {
        string g = DatabaseConfig.Schema_Global;
        DateTimeOffset now = DateTimeOffset.UtcNow;

        foreach ((Guid id, string code, string name) in UserTypes)
        {
            Execute.Sql($"""
INSERT INTO {g}.{UserTypesTable}
    (id, code, name, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{code}', '{name}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {g}.{UserTypesTable} WHERE code = '{code}'
);
""");
        }

        Execute.Sql($"""
UPDATE {g}.{DatabaseConfig.TableUserSchoolMappings} m
SET usertypeid = ut.id,
    updatedby = '{SeedActor}',
    updatedon = '{now:O}',
    versionno = m.versionno + 1
FROM {g}.{DatabaseConfig.TableUsers} u
INNER JOIN {g}.{DatabaseConfig.TableUserRoles} ur ON ur.userid = u.id AND ur.isactive = true
INNER JOIN {g}.{DatabaseConfig.TableRoles} r ON r.id = ur.roleid AND r.isactive = true
INNER JOIN {g}.{UserTypesTable} ut ON ut.code = '{UserTypeCodes.Student}'
WHERE m.userid = u.id
  AND m.isactive = true
  AND m.usertypeid IS NULL
  AND r.code = '{RoleCodes.Student}';
""");

        Execute.Sql($"""
UPDATE {g}.{DatabaseConfig.TableUserSchoolMappings} m
SET usertypeid = ut.id,
    updatedby = '{SeedActor}',
    updatedon = '{now:O}',
    versionno = m.versionno + 1
FROM {g}.{DatabaseConfig.TableUsers} u
INNER JOIN {g}.{DatabaseConfig.TableUserRoles} ur ON ur.userid = u.id AND ur.isactive = true
INNER JOIN {g}.{DatabaseConfig.TableRoles} r ON r.id = ur.roleid AND r.isactive = true
INNER JOIN {g}.{UserTypesTable} ut ON ut.code = '{UserTypeCodes.Parent}'
WHERE m.userid = u.id
  AND m.isactive = true
  AND m.usertypeid IS NULL
  AND r.code = '{RoleCodes.Parent}';
""");
    }

    public override void Down()
    {
        string g = DatabaseConfig.Schema_Global;
        string codes = string.Join("','", UserTypes.Select(t => t.Code));
        Execute.Sql($"""
UPDATE {g}.{DatabaseConfig.TableUserSchoolMappings}
SET usertypeid = NULL
WHERE usertypeid IN (SELECT id FROM {g}.{UserTypesTable} WHERE code IN ('{codes}'));
DELETE FROM {g}.{UserTypesTable} WHERE code IN ('{codes}');
""");
    }
}
