using SmartOps.Application.Modules.Authorization.Interfaces;

namespace SmartOps.Api.Middleware;

public sealed class UserScopeMiddleware
{
    private readonly RequestDelegate _next;

    public UserScopeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IUserScopeContext scopeContext)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await scopeContext.EnsureLoadedAsync(context.RequestAborted).ConfigureAwait(false);
        }

        await _next(context).ConfigureAwait(false);
    }
}
