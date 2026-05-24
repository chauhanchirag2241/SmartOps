namespace SmartOps.Application.Modules.Authorization.Interfaces;

public interface IDashboardWidgetPermissionService
{
    Task<IReadOnlyList<DashboardWidgetLayoutItemDto>> GetVisibleWidgetsAsync(CancellationToken cancellationToken = default);
}
