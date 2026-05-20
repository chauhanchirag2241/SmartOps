using Microsoft.AspNetCore.Authorization;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Authorization;

public sealed class MenuAnyPermissionRequirement : IAuthorizationRequirement
{
    public MenuAnyPermissionRequirement(params (string MenuCode, MenuPermissionAction Action)[] options)
    {
        Options = options;
    }

    public IReadOnlyList<(string MenuCode, MenuPermissionAction Action)> Options { get; }
}
