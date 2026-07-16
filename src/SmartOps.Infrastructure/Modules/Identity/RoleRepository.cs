using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Identity;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.Modules.Identity;

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
    code AS Code,
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
    code AS Code,
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
    code,
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
    @Code,
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
    code = @Code,
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
                    role.Code,
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
    code AS Code,
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

    public async Task<IReadOnlyList<RoleMenuPermissionDto>> GetMenuPermissionsForRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    m.id AS MenuId,
    m.code AS MenuCode,
    m.name AS MenuName,
    m.parentmenuid AS ParentMenuId,
    m.displayorder AS DisplayOrder,
    COALESCE(rmp.canview, false) AS CanView,
    COALESCE(rmp.canadd, false) AS CanAdd,
    COALESCE(rmp.canedit, false) AS CanEdit,
    COALESCE(rmp.candelete, false) AS CanDelete,
    COALESCE(rmp.canexport, false) AS CanExport
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
LEFT JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rmp
    ON rmp.menuid = m.id AND rmp.roleid = @RoleId AND rmp.isactive = true
WHERE m.isactive = true
ORDER BY m.displayorder, m.name
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<RoleMenuPermissionDto> rows = await connection.QueryAsync<RoleMenuPermissionDto>(
            new CommandDefinition(sql, new { RoleId = roleId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task SetRoleMenuPermissionsAsync(
        Guid roleId,
        IReadOnlyList<RoleMenuPermissionDto> permissions,
        CancellationToken cancellationToken = default)
    {
        if (permissions.Count == 0)
        {
            return;
        }

        Guid actor = ResolveUpdateActor();
        DateTime utcNow = DateTime.UtcNow;
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        string upsertSql = $"""
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), @RoleId, m.id, @CanView, @CanAdd, @CanEdit, @CanDelete, @CanExport, true, 1, @Actor, @Now, @Actor, @Now
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE m.code = @MenuCode AND m.isactive = true
ON CONFLICT ON CONSTRAINT uq_role_menu_permissions_role_menu
DO UPDATE SET
    canview = EXCLUDED.canview,
    canadd = EXCLUDED.canadd,
    canedit = EXCLUDED.canedit,
    candelete = EXCLUDED.candelete,
    canexport = EXCLUDED.canexport,
    isactive = true,
    updatedby = @Actor,
    updatedon = @Now,
    versionno = {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions}.versionno + 1
""";

        using IDbTransaction transaction = connection.BeginTransaction();
        try
        {
            foreach (RoleMenuPermissionDto permission in permissions)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        upsertSql,
                        new
                        {
                            RoleId = roleId,
                            permission.MenuCode,
                            permission.CanView,
                            permission.CanAdd,
                            permission.CanEdit,
                            permission.CanDelete,
                            permission.CanExport,
                            Actor = actor,
                            Now = utcNow
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken)).ConfigureAwait(false);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
