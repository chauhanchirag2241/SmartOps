using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.MultiTenancy;

/// <summary>
/// Identity tables copied into each dedicated school database <c>global</c> schema.
/// </summary>
internal static class IdentitySchemaCatalog
{
    internal static readonly string[] Tables =
    [
        DatabaseConfig.TableUsers,
        DatabaseConfig.TableRoles,
        DatabaseConfig.TableMenus,
        DatabaseConfig.TableUserRoles,
        DatabaseConfig.TableRoleMenuPermissions,
        DatabaseConfig.TableDashboardWidgets,
        DatabaseConfig.TableRoleDashboardWidgetPermissions,
        DatabaseConfig.TableUserTypes,
        DatabaseConfig.TableUserSchoolMappings,
        DatabaseConfig.TableSchoolSettings,
        DatabaseConfig.TableRefreshTokens,
        DatabaseConfig.TableUserScopeVersions,
    ];
}
