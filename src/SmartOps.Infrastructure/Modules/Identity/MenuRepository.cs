using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Identity;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.Persistence.Context;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Modules.Identity;

public sealed class MenuRepository : BaseRepository, IMenuRepository
{
    public MenuRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    public async Task<IReadOnlyList<Menu>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    id AS Id,
    name AS Name,
    code AS Code,
    parentmenuid AS ParentMenuId,
    route AS Route,
    icon AS Icon,
    displayorder AS DisplayOrder,
    application AS Application,
    isactive AS IsActive,
    versionno AS VersionNo,
    createdby AS CreatedBy,
    createdon AS CreatedOn,
    updatedby AS UpdatedBy,
    updatedon AS UpdatedOn
FROM {CatalogSchema}.{DatabaseConfig.TableMenus}
WHERE isactive = true
ORDER BY displayorder, name
""";

        IDbConnection connection = await Context.GetGlobalDatabaseConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<Menu> rows = await connection.QueryAsync<Menu>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public Task<IReadOnlyList<MenuPermissionDto>> GetUserMenuPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        QueryUserMenuPermissionsAsync(userId, application: null, cancellationToken);

    public Task<IReadOnlyList<MenuPermissionDto>> GetUserMenuPermissionsForApplicationAsync(
        Guid userId,
        string application,
        CancellationToken cancellationToken = default) =>
        QueryUserMenuPermissionsAsync(userId, application, cancellationToken);

    private async Task<IReadOnlyList<MenuPermissionDto>> QueryUserMenuPermissionsAsync(
        Guid userId,
        string? application,
        CancellationToken cancellationToken)
    {
        IDbConnection catalog = await Context.GetGlobalDatabaseConnectionAsync(cancellationToken).ConfigureAwait(false);
        IDbConnection tenant = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        string menusSql = $"""
SELECT id AS Id, code AS Code, application AS Application
FROM {CatalogSchema}.{DatabaseConfig.TableMenus}
WHERE isactive = true
""";
        List<MenuLookupRow> menus = (await catalog.QueryAsync<MenuLookupRow>(
            new CommandDefinition(menusSql, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        if (application is not null)
        {
            menus = menus
                .Where(m => m.Application == application || m.Application == MenuApplications.Common)
                .ToList();
        }

        if (menus.Count == 0)
        {
            return Array.Empty<MenuPermissionDto>();
        }

        string permsSql = $"""
SELECT
    rmp.menuid AS MenuId,
    bool_or(rmp.canview) AS CanView,
    bool_or(rmp.canadd) AS CanAdd,
    bool_or(rmp.canedit) AS CanEdit,
    bool_or(rmp.candelete) AS CanDelete,
    bool_or(rmp.canexport) AS CanExport
FROM {IdentitySchema}.{DatabaseConfig.TableRoleMenuPermissions} rmp
INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUserRoles} ur ON ur.roleid = rmp.roleid
WHERE ur.userid = @UserId
  AND ur.isactive = true
  AND rmp.isactive = true
GROUP BY rmp.menuid
""";
        Dictionary<Guid, PermissionAggRow> perms = (await tenant.QueryAsync<PermissionAggRow>(
            new CommandDefinition(permsSql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToDictionary(p => p.MenuId);

        return menus
            .Where(m => perms.ContainsKey(m.Id))
            .Select(m =>
            {
                PermissionAggRow p = perms[m.Id];
                return new MenuPermissionDto
                {
                    MenuCode = m.Code,
                    CanView = p.CanView,
                    CanAdd = p.CanAdd,
                    CanEdit = p.CanEdit,
                    CanDelete = p.CanDelete,
                    CanExport = p.CanExport,
                };
            })
            .OrderBy(m => m.MenuCode)
            .ToList();
    }

    public async Task<IReadOnlyList<MenuDto>> GetUserMenuTreeAsync(
        Guid userId,
        string application,
        CancellationToken cancellationToken = default)
    {
        IDbConnection tenant = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        string visibleSql = $"""
SELECT DISTINCT rmp.menuid AS MenuId
FROM {IdentitySchema}.{DatabaseConfig.TableRoleMenuPermissions} rmp
INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUserRoles} ur ON ur.roleid = rmp.roleid
WHERE ur.userid = @UserId
  AND ur.isactive = true
  AND rmp.isactive = true
  AND rmp.canview = true
""";
        HashSet<Guid> visibleIds = (await tenant.QueryAsync<Guid>(
            new CommandDefinition(visibleSql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToHashSet();

        if (visibleIds.Count == 0)
        {
            return Array.Empty<MenuDto>();
        }

        IReadOnlyList<Menu> allMenus = await GetActiveForApplicationAsync(application, cancellationToken).ConfigureAwait(false);
        HashSet<Guid> includedIds = allMenus
            .Where(m => visibleIds.Contains(m.Id))
            .Select(m => m.Id)
            .ToHashSet();

        foreach (Menu menu in allMenus.Where(m => includedIds.Contains(m.Id)))
        {
            AddParentChain(allMenus, menu.ParentMenuId, includedIds);
        }

        List<MenuRow> treeRows = allMenus
            .Where(m => includedIds.Contains(m.Id))
            .Select(m => new MenuRow
            {
                Id = m.Id,
                Name = m.Name,
                Code = m.Code,
                ParentMenuId = m.ParentMenuId,
                Route = m.Route,
                Icon = m.Icon,
                DisplayOrder = m.DisplayOrder
            })
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToList();

        return BuildTree(treeRows);
    }

    private async Task<IReadOnlyList<Menu>> GetActiveForApplicationAsync(
        string application,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Menu> all = await GetAllActiveAsync(cancellationToken).ConfigureAwait(false);
        return all
            .Where(m => m.Application == application || m.Application == MenuApplications.Common)
            .ToList();
    }

    private static void AddParentChain(IReadOnlyList<Menu> allMenus, Guid? parentId, ISet<Guid> includedIds)
    {
        while (parentId.HasValue)
        {
            Menu? parent = allMenus.FirstOrDefault(m => m.Id == parentId.Value);
            if (parent is null)
            {
                break;
            }

            includedIds.Add(parent.Id);
            parentId = parent.ParentMenuId;
        }
    }

    public Task<IReadOnlyList<RoleMenuPermissionDto>> GetAllMenuTemplatesAsync(
        CancellationToken cancellationToken = default) =>
        GetAllMenuTemplatesAsync(application: null, cancellationToken);

    public async Task<IReadOnlyList<RoleMenuPermissionDto>> GetAllMenuTemplatesAsync(
        string? application,
        CancellationToken cancellationToken = default)
    {
        string appFilter = string.Empty;
        object? args = null;
        if (!string.IsNullOrWhiteSpace(application))
        {
            appFilter = """
 AND (m.application = @Application OR m.application = @Common)
""";
            args = new { Application = application.Trim(), Common = MenuApplications.Common };
        }

        string sql = $"""
SELECT
    m.id AS MenuId,
    m.code AS MenuCode,
    m.name AS MenuName,
    m.parentmenuid AS ParentMenuId,
    m.displayorder AS DisplayOrder,
    m.application AS Application,
    false AS CanView,
    false AS CanAdd,
    false AS CanEdit,
    false AS CanDelete,
    false AS CanExport
FROM {CatalogSchema}.{DatabaseConfig.TableMenus} m
WHERE m.isactive = true{appFilter}
ORDER BY m.displayorder, m.name
""";

        IDbConnection connection = await Context.GetGlobalDatabaseConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<RoleMenuPermissionDto> rows = await connection.QueryAsync<RoleMenuPermissionDto>(
            new CommandDefinition(sql, args, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    private static IReadOnlyList<MenuDto> BuildTree(IReadOnlyList<MenuRow> rows)
    {
        Dictionary<Guid, MenuDto> nodes = rows.ToDictionary(
            r => r.Id,
            r => new MenuDto
            {
                Id = r.Id,
                Name = r.Name,
                Code = r.Code,
                Route = r.Route,
                Icon = r.Icon,
                DisplayOrder = r.DisplayOrder,
                Children = []
            });

        List<MenuDto> roots = new();
        foreach (MenuRow row in rows)
        {
            MenuDto node = nodes[row.Id];
            if (row.ParentMenuId is null || !nodes.TryGetValue(row.ParentMenuId.Value, out MenuDto? parent))
            {
                roots.Add(node);
                continue;
            }

            List<MenuDto> children = parent.Children.ToList();
            children.Add(node);
            parent.Children = children;
        }

        SortTree(roots);
        return roots;
    }

    private static void SortTree(List<MenuDto> nodes)
    {
        nodes.Sort((a, b) => a.DisplayOrder.CompareTo(b.DisplayOrder));
        foreach (MenuDto node in nodes)
        {
            if (node.Children is List<MenuDto> children)
            {
                SortTree(children);
            }
            else if (node.Children.Count > 0)
            {
                List<MenuDto> sorted = node.Children.ToList();
                SortTree(sorted);
                node.Children = sorted;
            }
        }
    }

    private sealed class MenuRow
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public Guid? ParentMenuId { get; set; }

        public string? Route { get; set; }

        public string? Icon { get; set; }

        public int DisplayOrder { get; set; }
    }

    private sealed class MenuLookupRow
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Application { get; set; } = string.Empty;
    }

    private sealed class PermissionAggRow
    {
        public Guid MenuId { get; set; }

        public bool CanView { get; set; }

        public bool CanAdd { get; set; }

        public bool CanEdit { get; set; }

        public bool CanDelete { get; set; }

        public bool CanExport { get; set; }
    }
}
