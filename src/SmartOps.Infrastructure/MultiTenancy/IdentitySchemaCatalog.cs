using SmartOps.Domain.Common.Configuration;

namespace SmartOps.Infrastructure.MultiTenancy;

/// <summary>
/// School-local identity/management tables in the dedicated school database <c>man</c> schema.
/// Catalog tables (menus, dashboard_widgets, usertypes) remain on the platform <c>global</c> database.
/// </summary>
internal static class IdentitySchemaCatalog
{
    internal static readonly string[] Tables =
    [
        DatabaseConfig.TableUsers,
        DatabaseConfig.TableRoles,
        DatabaseConfig.TableUserRoles,
        DatabaseConfig.TableRoleMenuPermissions,
        DatabaseConfig.TableRoleDashboardWidgetPermissions,
        DatabaseConfig.TableSchoolSettings,
        DatabaseConfig.TableSchoolBranches,
        DatabaseConfig.TableUserBranchMappings,
        DatabaseConfig.TableRefreshTokens,
    ];
}
