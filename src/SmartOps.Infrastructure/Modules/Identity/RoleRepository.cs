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
    description AS Description,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {IdentitySchema}.{DatabaseConfig.TableRoles}
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
FROM {IdentitySchema}.{DatabaseConfig.TableRoles}
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

        DateTime utcNow = SchoolLocalTime.NowDateTime();
        EnsureInsertAudit(role, utcNow);

        string sql = $"""
INSERT INTO {IdentitySchema}.{DatabaseConfig.TableRoles}
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
        DateTime utcNow = SchoolLocalTime.NowDateTime();
        Guid actor = ResolveUpdateActor();

        string sql = $"""
UPDATE {IdentitySchema}.{DatabaseConfig.TableRoles}
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
FROM {IdentitySchema}.{DatabaseConfig.TableRoles}
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
        IDbConnection catalog = await Context.GetGlobalDatabaseConnectionAsync(cancellationToken).ConfigureAwait(false);
        string menusSql = $"""
SELECT
    m.id AS MenuId,
    m.code AS MenuCode,
    m.name AS MenuName,
    m.parentmenuid AS ParentMenuId,
    m.displayorder AS DisplayOrder
FROM {CatalogSchema}.{DatabaseConfig.TableMenus} m
WHERE m.isactive = true
ORDER BY m.displayorder, m.name
""";
        List<RoleMenuPermissionDto> menus = (await catalog.QueryAsync<RoleMenuPermissionDto>(
            new CommandDefinition(menusSql, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        IDbConnection tenant = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string permsSql = $"""
SELECT
    menuid AS MenuId,
    canview AS CanView,
    canadd AS CanAdd,
    canedit AS CanEdit,
    candelete AS CanDelete,
    canexport AS CanExport
FROM {IdentitySchema}.{DatabaseConfig.TableRoleMenuPermissions}
WHERE roleid = @RoleId AND isactive = true
""";
        Dictionary<Guid, PermissionFlags> perms = (await tenant.QueryAsync<PermissionFlags>(
            new CommandDefinition(permsSql, new { RoleId = roleId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToDictionary(p => p.MenuId);

        foreach (RoleMenuPermissionDto menu in menus)
        {
            if (perms.TryGetValue(menu.MenuId, out PermissionFlags? flags))
            {
                menu.CanView = flags.CanView;
                menu.CanAdd = flags.CanAdd;
                menu.CanEdit = flags.CanEdit;
                menu.CanDelete = flags.CanDelete;
                menu.CanExport = flags.CanExport;
            }
        }

        return menus;
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
        DateTime utcNow = SchoolLocalTime.NowDateTime();

        IDbConnection catalog = await Context.GetGlobalDatabaseConnectionAsync(cancellationToken).ConfigureAwait(false);
        string menusSql = $"""
SELECT id AS Id, code AS Code
FROM {CatalogSchema}.{DatabaseConfig.TableMenus}
WHERE isactive = true
""";
        List<MenuCodeRow> menuRows = (await catalog.QueryAsync<MenuCodeRow>(
            new CommandDefinition(menusSql, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        HashSet<Guid> activeMenuIds = menuRows.Select(m => m.Id).ToHashSet();
        // Same code can exist per application (e.g. USERS for Config + School); prefer first match as fallback only.
        Dictionary<string, Guid> menuIdsByCode = menuRows
            .GroupBy(m => m.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        IDbConnection tenant = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string upsertSql = $"""
INSERT INTO {IdentitySchema}.{DatabaseConfig.TableRoleMenuPermissions}
    (id, roleid, menuid, canview, canadd, canedit, candelete, canexport, isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES
    (gen_random_uuid(), @RoleId, @MenuId, @CanView, @CanAdd, @CanEdit, @CanDelete, @CanExport, true, 1, @Actor, @Now, @Actor, @Now)
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
    versionno = {IdentitySchema}.{DatabaseConfig.TableRoleMenuPermissions}.versionno + 1
""";

        using IDbTransaction transaction = tenant.BeginTransaction();
        try
        {
            foreach (RoleMenuPermissionDto permission in permissions)
            {
                Guid menuId = permission.MenuId;
                if (menuId != Guid.Empty)
                {
                    if (!activeMenuIds.Contains(menuId))
                    {
                        continue;
                    }
                }
                else if (string.IsNullOrWhiteSpace(permission.MenuCode)
                    || !menuIdsByCode.TryGetValue(permission.MenuCode, out menuId))
                {
                    continue;
                }

                await tenant.ExecuteAsync(
                    new CommandDefinition(
                        upsertSql,
                        new
                        {
                            RoleId = roleId,
                            MenuId = menuId,
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

    private sealed class PermissionFlags
    {
        public Guid MenuId { get; set; }

        public bool CanView { get; set; }

        public bool CanAdd { get; set; }

        public bool CanEdit { get; set; }

        public bool CanDelete { get; set; }

        public bool CanExport { get; set; }
    }

    private sealed class MenuCodeRow
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = string.Empty;
    }
}
