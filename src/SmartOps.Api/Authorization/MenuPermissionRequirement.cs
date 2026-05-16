using Microsoft.AspNetCore.Authorization;
using SmartOps.Shared.Constants;

namespace SmartOps.Api.Authorization;

public sealed class MenuPermissionRequirement : IAuthorizationRequirement
{
    public MenuPermissionRequirement(string menuCode, MenuPermissionAction action)
    {
        MenuCode = menuCode;
        Action = action;
    }

    public string MenuCode { get; }

    public MenuPermissionAction Action { get; }
}
