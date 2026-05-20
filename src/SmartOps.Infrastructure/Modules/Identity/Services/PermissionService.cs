using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartOps.Application.Modules.Identity;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Modules.Identity.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMenuRepository _menuRepository;
    private IReadOnlyList<MenuPermissionDto> _permissions = Array.Empty<MenuPermissionDto>();
    private bool _loaded;

    public PermissionService(IHttpContextAccessor httpContextAccessor, IMenuRepository menuRepository)
    {
        _httpContextAccessor = httpContextAccessor;
        _menuRepository = menuRepository;
    }

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
        {
            return;
        }

        Guid? userId = GetCurrentUserId();
        if (userId is null)
        {
            _permissions = Array.Empty<MenuPermissionDto>();
            _loaded = true;
            return;
        }

        _permissions = await _menuRepository
            .GetUserMenuPermissionsAsync(userId.Value, cancellationToken)
            .ConfigureAwait(false);
        _loaded = true;
    }

    public IReadOnlyList<MenuPermissionDto> GetPermissions() => _permissions;

    public MenuPermissionDto? GetPermission(string menuCode) =>
        _permissions.FirstOrDefault(p => string.Equals(p.MenuCode, menuCode, StringComparison.OrdinalIgnoreCase));

    public bool HasViewAccess(string menuCode) => HasAccess(menuCode, MenuPermissionAction.View);

    public bool HasAddAccess(string menuCode) => HasAccess(menuCode, MenuPermissionAction.Add);

    public bool HasEditAccess(string menuCode) => HasAccess(menuCode, MenuPermissionAction.Edit);

    public bool HasDeleteAccess(string menuCode) => HasAccess(menuCode, MenuPermissionAction.Delete);

    public bool HasExportAccess(string menuCode) => HasAccess(menuCode, MenuPermissionAction.Export);

    public bool HasAccess(string menuCode, MenuPermissionAction action)
    {
        MenuPermissionDto? permission = GetPermission(menuCode);
        if (permission is null)
        {
            return false;
        }

        return action switch
        {
            MenuPermissionAction.View => permission.CanView,
            MenuPermissionAction.Add => permission.CanAdd,
            MenuPermissionAction.Edit => permission.CanEdit,
            MenuPermissionAction.Delete => permission.CanDelete,
            MenuPermissionAction.Export => permission.CanExport,
            _ => false
        };
    }

    private Guid? GetCurrentUserId()
    {
        string? sub = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out Guid userId) ? userId : null;
    }
}
