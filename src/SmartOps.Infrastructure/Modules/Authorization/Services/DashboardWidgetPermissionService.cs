using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Identity;
using SmartOps.Application.Modules.Identity.Interfaces;

namespace SmartOps.Infrastructure.Modules.Authorization.Services;

public sealed class DashboardWidgetPermissionService : IDashboardWidgetPermissionService
{
    private readonly ICurrentUserService _currentUser;
    private readonly IDashboardWidgetRepository _widgetRepository;
    private readonly IPermissionService _permissionService;

    public DashboardWidgetPermissionService(
        ICurrentUserService currentUser,
        IDashboardWidgetRepository widgetRepository,
        IPermissionService permissionService)
    {
        _currentUser = currentUser;
        _widgetRepository = widgetRepository;
        _permissionService = permissionService;
    }

    public async Task<IReadOnlyList<DashboardWidgetLayoutItemDto>> GetVisibleWidgetsAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            return [];
        }

        await _permissionService.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<string> roleWidgetCodes = await _widgetRepository
            .GetUserWidgetCodesAsync(_currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (roleWidgetCodes.Count == 0)
        {
            return [];
        }

        IReadOnlyList<RoleDashboardWidgetPermissionDto> templates = await _widgetRepository
            .GetWidgetTemplatesAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<string> allowedCodes = roleWidgetCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> addedCodes = new(StringComparer.OrdinalIgnoreCase);
        List<DashboardWidgetLayoutItemDto> visible = new();

        foreach (RoleDashboardWidgetPermissionDto template in templates)
        {
            if (!allowedCodes.Contains(template.WidgetCode))
            {
                continue;
            }

            if (!addedCodes.Add(template.WidgetCode))
            {
                continue;
            }

            if (!_permissionService.HasViewAccess(template.RequiredMenuCode))
            {
                continue;
            }

            visible.Add(new DashboardWidgetLayoutItemDto
            {
                Code = template.WidgetCode,
                Name = template.WidgetName,
                Category = template.Category,
                DefaultSize = template.DefaultSize,
                DisplayOrder = template.DisplayOrder,
                RequiredMenuCode = template.RequiredMenuCode
            });
        }

        return visible;
    }
}
