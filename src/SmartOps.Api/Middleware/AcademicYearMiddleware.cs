using SmartOps.Application.Modules.AcademicYear;

namespace SmartOps.Api.Middleware;

public sealed class AcademicYearMiddleware
{
    private readonly RequestDelegate _next;

    public AcademicYearMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAcademicYearContext academicYearContext)
    {
        if (!ApiMiddlewarePaths.IsTenantContextBypass(context)
            && context.User.Identity?.IsAuthenticated == true)
        {
            await academicYearContext.EnsureResolvedAsync(context.RequestAborted).ConfigureAwait(false);
        }

        await _next(context).ConfigureAwait(false);
    }
}
