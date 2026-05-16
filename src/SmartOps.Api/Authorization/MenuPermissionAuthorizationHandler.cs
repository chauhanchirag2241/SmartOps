using Microsoft.AspNetCore.Authorization;
using SmartOps.Application.Modules.Identity.Interfaces;

namespace SmartOps.Api.Authorization;

public sealed class MenuPermissionAuthorizationHandler : AuthorizationHandler<MenuPermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public MenuPermissionAuthorizationHandler(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MenuPermissionRequirement requirement)
    {
        await _permissionService.EnsureLoadedAsync().ConfigureAwait(false);

        if (_permissionService.HasAccess(requirement.MenuCode, requirement.Action))
        {
            context.Succeed(requirement);
        }
    }
}
