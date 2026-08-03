using System.Text;
using Dapper;
using Npgsql;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;

namespace SmartOps.Infrastructure.MultiTenancy;

/// <summary>
/// Seeds default school portal roles (School Admin, Principal, Teacher, Accountant, Front Office Executive, SmartOpsAdmin)
/// and their menu permissions into <c>man.roles</c> / <c>man.rolemenupermissions</c>.
/// Student is not a portal role — mobile app role will be added separately.
/// </summary>
public static class SchoolDefaultRoleSeeder
{
    private static readonly Guid SeedActor = Guid.Parse(DatabaseConfig.SystemUserId);

    public static async Task EnsureDefaultRolesAsync(
        NpgsqlConnection schoolDb,
        CancellationToken cancellationToken = default)
    {
        string man = DatabaseConfig.Schema_Man;
        DateTime utcNow = SchoolLocalTime.NowDateTime();

        string insertRoleSql = $"""
INSERT INTO {man}.{DatabaseConfig.TableRoles}
    (id, name, description, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT @Id, @Name, @Description, true, 1, @Actor, @Now, @Actor, @Now
WHERE NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableRoles}
    WHERE lower(trim(name)) = lower(trim(@Name))
);
""";

        foreach ((Guid id, string name, string description) in RoleNames.Defaults)
        {
            await schoolDb.ExecuteAsync(
                new CommandDefinition(
                    insertRoleSql,
                    new { Id = id, Name = name, Description = description, Actor = SeedActor, Now = utcNow },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }

    public static async Task GrantDefaultRoleMenuPermissionsAsync(
        NpgsqlConnection schoolDb,
        CancellationToken cancellationToken = default)
    {
        string man = DatabaseConfig.Schema_Man;
        DateTime utcNow = SchoolLocalTime.NowDateTime();

        string insertPermSql = $"""
INSERT INTO {man}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, @MenuId, @CanView, @CanAdd, @CanEdit, @CanDelete, @CanExport, true, 1, @Actor, @Now, @Actor, @Now
FROM {man}.{DatabaseConfig.TableRoles} r
WHERE lower(trim(r.name)) = lower(trim(@RoleName))
  AND NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = @MenuId
  );
""";

        foreach ((string roleName, DefaultSchoolRolePermissions.Grant[] grants) in DefaultSchoolRolePermissions.ByRoleName)
        {
            foreach (DefaultSchoolRolePermissions.Grant grant in grants)
            {
                await schoolDb.ExecuteAsync(
                    new CommandDefinition(
                        insertPermSql,
                        new
                        {
                            RoleName = roleName,
                            MenuId = grant.MenuId,
                            CanView = grant.CanView,
                            CanAdd = grant.CanAdd,
                            CanEdit = grant.CanEdit,
                            CanDelete = grant.CanDelete,
                            CanExport = grant.CanExport,
                            Actor = SeedActor,
                            Now = utcNow,
                        },
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
        }
    }

    /// <summary>SQL batch for FluentMigrator school migrations (idempotent).</summary>
    public static string BuildMigrationSql(DateTimeOffset now)
    {
        string man = DatabaseConfig.Schema_Man;
        var sb = new StringBuilder();

        foreach ((Guid id, string name, string description) in RoleNames.Defaults)
        {
            string desc = description.Replace("'", "''", StringComparison.Ordinal);
            sb.AppendLine($"""
INSERT INTO {man}.{DatabaseConfig.TableRoles}
    (id, name, description, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT '{id}', '{name}', '{desc}', true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
WHERE NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableRoles}
    WHERE lower(trim(name)) = lower(trim('{name}'))
);
""");
        }

        foreach ((string roleName, DefaultSchoolRolePermissions.Grant[] grants) in DefaultSchoolRolePermissions.ByRoleName)
        {
            foreach (DefaultSchoolRolePermissions.Grant grant in grants)
            {
                sb.AppendLine($"""
INSERT INTO {man}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), r.id, '{grant.MenuId}', {(grant.CanView ? "true" : "false")}, {(grant.CanAdd ? "true" : "false")}, {(grant.CanEdit ? "true" : "false")}, {(grant.CanDelete ? "true" : "false")}, {(grant.CanExport ? "true" : "false")}, true, 1, '{SeedActor}', '{now:O}', '{SeedActor}', '{now:O}'
FROM {man}.{DatabaseConfig.TableRoles} r
WHERE lower(trim(r.name)) = lower(trim('{roleName}'))
  AND NOT EXISTS (
    SELECT 1 FROM {man}.{DatabaseConfig.TableRoleMenuPermissions} rp
    WHERE rp.roleid = r.id AND rp.menuid = '{grant.MenuId}'
  );
""");
            }
        }

        return sb.ToString();
    }

    public static string BuildMigrationDownSql()
    {
        string man = DatabaseConfig.Schema_Man;
        string nonAdmin = string.Join(
            "','",
            RoleNames.Defaults
                .Where(r => !RoleNames.IsFullAccessRole(r.Name))
                .Select(r => r.Name.ToLowerInvariant()));

        return $"""
DELETE FROM {man}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE roleid IN (
    SELECT id FROM {man}.{DatabaseConfig.TableRoles}
    WHERE lower(trim(name)) IN ('{nonAdmin}')
);
DELETE FROM {man}.{DatabaseConfig.TableRoles}
WHERE lower(trim(name)) IN ('{nonAdmin}');
""";
    }
}
