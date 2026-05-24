using SmartOps.Application.Modules.Identity;

namespace SmartOps.Application.Modules.Authorization.Interfaces;

public interface IDashboardWidgetRepository
{
    Task<IReadOnlyList<RoleDashboardWidgetPermissionDto>> GetWidgetTemplatesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleDashboardWidgetPermissionDto>> GetWidgetPermissionsForRoleAsync(
        Guid roleId,
        CancellationToken cancellationToken = default);

    Task SetRoleWidgetPermissionsAsync(
        Guid roleId,
        IReadOnlyList<RoleDashboardWidgetPermissionDto> permissions,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetUserWidgetCodesAsync(Guid userId, CancellationToken cancellationToken = default);
}
