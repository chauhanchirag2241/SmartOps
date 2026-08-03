using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Common;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Identity;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Authorization;

public sealed class DashboardWidgetRepository : BaseRepository, IDashboardWidgetRepository
{
    public DashboardWidgetRepository(DapperContext context, ICurrentUserService currentUser)
        : base(context, currentUser)
    {
    }

    private bool IsSchoolTenant =>
        !string.Equals(IdentitySchema, CatalogSchema, StringComparison.Ordinal);

    public async Task<IReadOnlyList<RoleDashboardWidgetPermissionDto>> GetWidgetTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        return await LoadWidgetCatalogAsync(canViewByWidgetId: null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RoleDashboardWidgetPermissionDto>> GetWidgetPermissionsForRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        Dictionary<Guid, bool>? canViewByWidgetId = null;
        if (IsSchoolTenant)
        {
            IDbConnection tenant = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
            string permsSql = $"""
SELECT widgetid AS WidgetId, canview AS CanView
FROM {IdentitySchema}.{DatabaseConfig.TableRoleDashboardWidgetPermissions}
WHERE roleid = @RoleId AND isactive = true
""";
            canViewByWidgetId = (await tenant.QueryAsync<WidgetPermissionRow>(
                new CommandDefinition(permsSql, new { RoleId = roleId }, cancellationToken: cancellationToken))
                .ConfigureAwait(false)).ToDictionary(r => r.WidgetId, r => r.CanView);
        }

        return await LoadWidgetCatalogAsync(canViewByWidgetId, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetRoleWidgetPermissionsAsync(
        Guid roleId,
        IReadOnlyList<RoleDashboardWidgetPermissionDto> permissions,
        CancellationToken cancellationToken = default)
    {
        if (permissions.Count == 0 || !IsSchoolTenant)
        {
            return;
        }

        Guid actor = ResolveUpdateActor();
        DateTime utcNow = SchoolLocalTime.NowDateTime();

        IDbConnection catalog = await Context.GetGlobalDatabaseConnectionAsync(cancellationToken).ConfigureAwait(false);
        string widgetsSql = $"""
SELECT id AS Id, code AS Code
FROM {CatalogSchema}.{DatabaseConfig.TableDashboardWidgets}
WHERE isactive = true
""";
        Dictionary<string, Guid> widgetIdsByCode = (await catalog.QueryAsync<CodeIdRow>(
            new CommandDefinition(widgetsSql, cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToDictionary(w => w.Code, w => w.Id, StringComparer.OrdinalIgnoreCase);

        IDbConnection tenant = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string upsertSql = $"""
INSERT INTO {IdentitySchema}.{DatabaseConfig.TableRoleDashboardWidgetPermissions}
    (id, roleid, widgetid, canview, isactive, versionno, createdby, createdon, updatedby, updatedon)
VALUES
    (gen_random_uuid(), @RoleId, @WidgetId, @CanView, true, 1, @Actor, @Now, @Actor, @Now)
ON CONFLICT ON CONSTRAINT uq_role_dashboard_widget_permissions_role_widget
DO UPDATE SET
    canview = EXCLUDED.canview,
    isactive = true,
    updatedby = @Actor,
    updatedon = @Now,
    versionno = {IdentitySchema}.{DatabaseConfig.TableRoleDashboardWidgetPermissions}.versionno + 1
""";

        using IDbTransaction transaction = tenant.BeginTransaction();
        try
        {
            foreach (RoleDashboardWidgetPermissionDto permission in permissions)
            {
                if (!widgetIdsByCode.TryGetValue(permission.WidgetCode, out Guid widgetId))
                {
                    continue;
                }

                await tenant.ExecuteAsync(
                    new CommandDefinition(
                        upsertSql,
                        new
                        {
                            RoleId = roleId,
                            WidgetId = widgetId,
                            permission.CanView,
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

    public async Task<IReadOnlyList<string>> GetUserWidgetCodesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (!IsSchoolTenant)
        {
            return Array.Empty<string>();
        }

        IDbConnection tenant = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        string permsSql = $"""
SELECT rdwp.widgetid AS WidgetId
FROM {IdentitySchema}.{DatabaseConfig.TableRoleDashboardWidgetPermissions} rdwp
INNER JOIN {IdentitySchema}.{DatabaseConfig.TableUserRoles} ur
    ON ur.roleid = rdwp.roleid AND ur.isactive = true
WHERE ur.userid = @UserId
  AND rdwp.isactive = true
  AND rdwp.canview = true
GROUP BY rdwp.widgetid
""";
        HashSet<Guid> allowedWidgetIds = (await tenant.QueryAsync<Guid>(
            new CommandDefinition(permsSql, new { UserId = userId }, cancellationToken: cancellationToken))
            .ConfigureAwait(false)).ToHashSet();

        if (allowedWidgetIds.Count == 0)
        {
            return Array.Empty<string>();
        }

        IDbConnection catalog = await Context.GetGlobalDatabaseConnectionAsync(cancellationToken).ConfigureAwait(false);
        string widgetsSql = $"""
SELECT id AS Id, code AS Code, displayorder AS DisplayOrder
FROM {CatalogSchema}.{DatabaseConfig.TableDashboardWidgets}
WHERE isactive = true
ORDER BY displayorder
""";
        List<WidgetCodeRow> widgets =
            (await catalog.QueryAsync<WidgetCodeRow>(
                new CommandDefinition(widgetsSql, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        return widgets
            .Where(w => allowedWidgetIds.Contains(w.Id))
            .OrderBy(w => w.DisplayOrder)
            .Select(w => w.Code)
            .ToList();
    }

    private async Task<IReadOnlyList<RoleDashboardWidgetPermissionDto>> LoadWidgetCatalogAsync(
        IReadOnlyDictionary<Guid, bool>? canViewByWidgetId,
        CancellationToken cancellationToken)
    {
        string sql = $"""
SELECT
    w.id AS WidgetId,
    w.code AS WidgetCode,
    w.name AS WidgetName,
    w.category AS Category,
    w.requiredmenucode AS RequiredMenuCode,
    w.displayorder AS DisplayOrder,
    w.defaultsize AS DefaultSize
FROM {CatalogSchema}.{DatabaseConfig.TableDashboardWidgets} w
WHERE w.isactive = true
ORDER BY w.displayorder, w.name
""";

        IDbConnection catalog = await Context.GetGlobalDatabaseConnectionAsync(cancellationToken).ConfigureAwait(false);
        List<RoleDashboardWidgetPermissionDto> rows = (await catalog.QueryAsync<RoleDashboardWidgetPermissionDto>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false)).ToList();

        if (canViewByWidgetId is null)
        {
            foreach (RoleDashboardWidgetPermissionDto row in rows)
            {
                row.CanView = false;
            }

            return rows;
        }

        foreach (RoleDashboardWidgetPermissionDto row in rows)
        {
            row.CanView = canViewByWidgetId.TryGetValue(row.WidgetId, out bool canView) && canView;
        }

        return rows;
    }

    private sealed class WidgetPermissionRow
    {
        public Guid WidgetId { get; set; }

        public bool CanView { get; set; }
    }

    private sealed class CodeIdRow
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = string.Empty;
    }

    private sealed class WidgetCodeRow
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public int DisplayOrder { get; set; }
    }
}
