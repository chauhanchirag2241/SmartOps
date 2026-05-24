using System.Data;
using Dapper;
using SmartOps.Application.Abstractions;
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

    public async Task<IReadOnlyList<RoleDashboardWidgetPermissionDto>> GetWidgetTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        return await QueryWidgetPermissionsAsync(null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RoleDashboardWidgetPermissionDto>> GetWidgetPermissionsForRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        return await QueryWidgetPermissionsAsync(roleId, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetRoleWidgetPermissionsAsync(
        Guid roleId,
        IReadOnlyList<RoleDashboardWidgetPermissionDto> permissions,
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
INSERT INTO {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleDashboardWidgetPermissions}
    (id, roleid, widgetid, canview, isactive, versionno, createdby, createdon, updatedby, updatedon)
SELECT gen_random_uuid(), @RoleId, w.id, @CanView, true, 1, @Actor, @Now, @Actor, @Now
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableDashboardWidgets} w
WHERE w.code = @WidgetCode AND w.isactive = true
ON CONFLICT ON CONSTRAINT uq_role_dashboard_widget_permissions_role_widget
DO UPDATE SET
    canview = EXCLUDED.canview,
    isactive = true,
    updatedby = @Actor,
    updatedon = @Now,
    versionno = {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleDashboardWidgetPermissions}.versionno + 1
""";

        using IDbTransaction transaction = connection.BeginTransaction();
        try
        {
            foreach (RoleDashboardWidgetPermissionDto permission in permissions)
            {
                await connection.ExecuteAsync(
                    new CommandDefinition(
                        upsertSql,
                        new
                        {
                            RoleId = roleId,
                            permission.WidgetCode,
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
        string sql = $"""
SELECT w.code AS WidgetCode
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableDashboardWidgets} w
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleDashboardWidgetPermissions} rdwp
    ON rdwp.widgetid = w.id AND rdwp.isactive = true AND rdwp.canview = true
INNER JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableUserRoles} ur
    ON ur.roleid = rdwp.roleid AND ur.isactive = true
WHERE ur.userid = @UserId
  AND w.isactive = true
GROUP BY w.code, w.displayorder
ORDER BY w.displayorder
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<string> rows = await connection.QueryAsync<string>(
            new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }

    private async Task<IReadOnlyList<RoleDashboardWidgetPermissionDto>> QueryWidgetPermissionsAsync(
        Guid? roleId,
        CancellationToken cancellationToken)
    {
        string sql = roleId.HasValue
            ? $"""
SELECT
    w.id AS WidgetId,
    w.code AS WidgetCode,
    w.name AS WidgetName,
    w.category AS Category,
    w.requiredmenucode AS RequiredMenuCode,
    w.displayorder AS DisplayOrder,
    w.defaultsize AS DefaultSize,
    COALESCE(rdwp.canview, false) AS CanView
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableDashboardWidgets} w
LEFT JOIN {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableRoleDashboardWidgetPermissions} rdwp
    ON rdwp.widgetid = w.id AND rdwp.isactive = true AND rdwp.roleid = @RoleId
WHERE w.isactive = true
ORDER BY w.displayorder, w.name
"""
            : $"""
SELECT
    w.id AS WidgetId,
    w.code AS WidgetCode,
    w.name AS WidgetName,
    w.category AS Category,
    w.requiredmenucode AS RequiredMenuCode,
    w.displayorder AS DisplayOrder,
    w.defaultsize AS DefaultSize,
    false AS CanView
FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableDashboardWidgets} w
WHERE w.isactive = true
ORDER BY w.displayorder, w.name
""";

        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        IEnumerable<RoleDashboardWidgetPermissionDto> rows = await connection.QueryAsync<RoleDashboardWidgetPermissionDto>(
            new CommandDefinition(sql, new { RoleId = roleId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        return rows.ToList();
    }
}
