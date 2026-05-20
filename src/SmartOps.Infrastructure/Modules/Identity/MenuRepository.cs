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
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus}
WHERE isactive = true
ORDER BY displayorder, name
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
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
        string applicationFilter = application is null
            ? string.Empty
            : "AND m.application IN (@Application, @Common)";

        string sql = $"""
SELECT
    m.code AS MenuCode,
    bool_or(rmp.canview) AS CanView,
    bool_or(rmp.canadd) AS CanAdd,
    bool_or(rmp.canedit) AS CanEdit,
    bool_or(rmp.candelete) AS CanDelete,
    bool_or(rmp.canexport) AS CanExport
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rmp ON rmp.menuid = m.id
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles} ur ON ur.roleid = rmp.roleid
WHERE ur.userid = @UserId
  AND ur.isactive = true
  AND rmp.isactive = true
  AND m.isactive = true
  {applicationFilter}
GROUP BY m.code
ORDER BY m.code
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<MenuPermissionDto> rows = await connection.QueryAsync<MenuPermissionDto>(
            new CommandDefinition(
                sql,
                new { UserId = userId, Application = application, Common = MenuApplications.Common },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MenuDto>> GetUserMenuTreeAsync(
        Guid userId,
        string application,
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT DISTINCT
    m.id AS Id,
    m.name AS Name,
    m.code AS Code,
    m.parentmenuid AS ParentMenuId,
    m.route AS Route,
    m.icon AS Icon,
    m.displayorder AS DisplayOrder
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleMenuPermissions} rmp ON rmp.menuid = m.id
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles} ur ON ur.roleid = rmp.roleid
WHERE ur.userid = @UserId
  AND ur.isactive = true
  AND rmp.isactive = true
  AND m.isactive = true
  AND rmp.canview = true
  AND m.application IN (@Application, @Common)
ORDER BY m.displayorder, m.name
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        List<MenuRow> visibleRows = (await connection.QueryAsync<MenuRow>(
            new CommandDefinition(
                sql,
                new { UserId = userId, Application = application, Common = MenuApplications.Common },
                cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        if (visibleRows.Count == 0)
        {
            return Array.Empty<MenuDto>();
        }

        IReadOnlyList<Menu> allMenus = await GetActiveForApplicationAsync(application, cancellationToken).ConfigureAwait(false);
        HashSet<Guid> includedIds = visibleRows.Select(r => r.Id).ToHashSet();

        foreach (MenuRow row in visibleRows)
        {
            AddParentChain(allMenus, row.ParentMenuId, includedIds);
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

    public async Task<IReadOnlyList<RoleMenuPermissionDto>> GetAllMenuTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        string sql = $"""
SELECT
    m.id AS MenuId,
    m.code AS MenuCode,
    m.name AS MenuName,
    false AS CanView,
    false AS CanAdd,
    false AS CanEdit,
    false AS CanDelete,
    false AS CanExport
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableMenus} m
WHERE m.isactive = true
ORDER BY m.displayorder, m.name
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<RoleMenuPermissionDto> rows = await connection.QueryAsync<RoleMenuPermissionDto>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
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

        return roots;
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
}
