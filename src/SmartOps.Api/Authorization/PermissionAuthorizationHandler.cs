using Microsoft.AspNetCore.Authorization;
using SmartOps.Shared.Constants;

namespace SmartOps.Api.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.IsInRole("SchoolAdmin") || context.User.IsInRole("PlatformAdmin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        string? permissionsClaim = context.User.FindFirst(JwtCustomClaimNames.Permissions)?.Value;
        if (string.IsNullOrWhiteSpace(permissionsClaim))
        {
            return Task.CompletedTask;
        }

        HashSet<string> permissions = permissionsClaim
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);

        if (permissions.Contains(PermissionNames.AdminFull) || permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
