using System.Data;
using Dapper;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence.Repositories;
using SmartOps.Shared.Common;
using SmartOps.Shared.Configuration;

namespace SmartOps.Infrastructure.Modules.Identity.Repositories;

public sealed class RoleRepository : BaseRepository, IRoleRepository
{
    public RoleRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<ApplicationRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    id AS Id,
    name AS Name,
    description AS Description,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
WHERE id = @Id AND isactive = true
LIMIT 1
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<ApplicationRole>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<ApplicationRole?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    id AS Id,
    name AS Name,
    description AS Description,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
WHERE name = @Name AND isactive = true
LIMIT 1
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await connection.QuerySingleOrDefaultAsync<ApplicationRole>(
            new CommandDefinition(sql, new { Name = name }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task CreateAsync(ApplicationRole role, CancellationToken cancellationToken = default)
    {
        if (role.Id == Guid.Empty)
        {
            role.Id = Guid.NewGuid();
        }

        DateTime utcNow = DateTime.UtcNow;
        EnsureInsertAudit(role, utcNow);

        string sql = $"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
(
    id,
    name,
    description,
    isactive,
    versionno,
    createdby,
    createdon,
    updatedby,
    updatedon
)
VALUES
(
    @Id,
    @Name,
    @Description,
    @IsActive,
    @VersionNo,
    @CreatedBy,
    @CreatedOn,
    @UpdatedBy,
    @UpdatedOn
)
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(sql, role, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task UpdateAsync(ApplicationRole role, CancellationToken cancellationToken = default)
    {
        DateTime utcNow = DateTime.UtcNow;
        Guid actor = ResolveUpdateActor();

        string sql = $"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
SET
    name = @Name,
    description = @Description,
    isactive = @IsActive,
    updatedby = @UpdatedBy,
    updatedon = @UpdatedOn,
    versionno = versionno + 1
WHERE id = @Id AND versionno = @VersionNo AND isactive = true
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        int rows = await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    role.Id,
                    role.Name,
                    role.Description,
                    role.IsActive,
                    UpdatedBy = actor,
                    UpdatedOn = utcNow,
                    VersionNo = role.VersionNo
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (rows == 0)
        {
            throw new ConcurrencyException("Record was modified by another user.");
        }

        role.VersionNo += 1;
        role.UpdatedBy = actor;
        role.UpdatedOn = utcNow;
    }

    public async Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    id AS Id,
    name AS Name,
    description AS Description,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoles}
WHERE isactive = true
ORDER BY name
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<ApplicationRole> rows = await connection.QueryAsync<ApplicationRole>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<string>> GetPermissionNamesForRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT p.name
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TablePermissions} p
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions} rp ON rp.permissionid = p.id
WHERE rp.roleid = @RoleId AND rp.isactive = true AND p.isactive = true
ORDER BY p.name
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<string> rows = await connection.QueryAsync<string>(
            new CommandDefinition(sql, new { RoleId = roleId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task SetRolePermissionsAsync(Guid roleId, IReadOnlyList<string> permissionNames, CancellationToken cancellationToken = default)
    {
        Guid actor = ResolveUpdateActor();
        DateTime utcNow = DateTime.UtcNow;
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        string deactivateSql = $"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions}
SET isactive = false, updatedby = @Actor, updatedon = @Now, versionno = versionno + 1
WHERE roleid = @RoleId AND isactive = true
""";
        await connection.ExecuteAsync(
            new CommandDefinition(deactivateSql, new { RoleId = roleId, Actor = actor, Now = utcNow }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        foreach (string permissionName in permissionNames.Distinct(StringComparer.Ordinal))
        {
            string insertSql = $"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions}
    (roleid, permissionid, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT @RoleId, p.id, true, 1, @Actor, @Now, @Actor, @Now
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TablePermissions} p
WHERE p.name = @PermissionName AND p.isactive = true
  AND NOT EXISTS (
    SELECT 1 FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions} rp
    WHERE rp.roleid = @RoleId AND rp.permissionid = p.id AND rp.isactive = true
  )
""";
            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    new { RoleId = roleId, PermissionName = permissionName, Actor = actor, Now = utcNow },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);

            string reviveSql = $"""
UPDATE {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRolePermissions}
SET isactive = true, updatedby = @Actor, updatedon = @Now, versionno = versionno + 1
WHERE roleid = @RoleId
  AND permissionid = (SELECT id FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TablePermissions} WHERE name = @PermissionName LIMIT 1)
  AND isactive = false
""";
            await connection.ExecuteAsync(
                new CommandDefinition(
                    reviveSql,
                    new { RoleId = roleId, PermissionName = permissionName, Actor = actor, Now = utcNow },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
    }
}
