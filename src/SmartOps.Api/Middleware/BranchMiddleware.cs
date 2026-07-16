using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Branch;
using SmartOps.Infrastructure.Modules.Branch;

namespace SmartOps.Api.Middleware;

public sealed class BranchMiddleware
{
    private readonly RequestDelegate _next;

    public BranchMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IBranchContext branchContext,
        SchoolBranchSyncService schoolBranchSyncService,
        ITenantProvider tenantProvider)
    {
        if (!ApiMiddlewarePaths.IsTenantContextBypass(context)
            && context.User.Identity?.IsAuthenticated == true)
        {
            if (Guid.TryParse(tenantProvider.GetCurrentSchoolId(), out Guid schoolId))
            {
                await schoolBranchSyncService
                    .EnsureSyncedAsync(schoolId, context.RequestAborted)
                    .ConfigureAwait(false);
            }

            await branchContext.EnsureResolvedAsync(context.RequestAborted).ConfigureAwait(false);

            if (branchContext.IsResolved
                && branchContext.ActiveBranchId is Guid activeBranchId
                && !branchContext.HasBranchAccess(activeBranchId))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Branch access denied.").ConfigureAwait(false);
                return;
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}
