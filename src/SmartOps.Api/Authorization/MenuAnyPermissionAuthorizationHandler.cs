using Microsoft.AspNetCore.Authorization;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Authorization;

public sealed class MenuAnyPermissionAuthorizationHandler : AuthorizationHandler<MenuAnyPermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public MenuAnyPermissionAuthorizationHandler(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MenuAnyPermissionRequirement requirement)
    {
        await _permissionService.EnsureLoadedAsync().ConfigureAwait(false);

        foreach ((string menuCode, MenuPermissionAction action) in requirement.Options)
        {
            if (_permissionService.HasAccess(menuCode, action))
            {
                context.Succeed(requirement);
                return;
            }
        }
    }
}
