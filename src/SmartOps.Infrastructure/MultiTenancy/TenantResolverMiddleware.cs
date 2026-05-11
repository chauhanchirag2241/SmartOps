using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using SmartOps.Shared.Constants;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class TenantResolverMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolverMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        string? tenantId = null;
        string? schoolId = null;

        string? authorization = context.Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            string rawToken = authorization["Bearer ".Length..].Trim();
            try
            {
                JwtSecurityTokenHandler handler = new();
                JwtSecurityToken jwt = handler.ReadJwtToken(rawToken);
                tenantId = jwt.Claims.FirstOrDefault(c => c.Type == JwtCustomClaimNames.TenantId)?.Value;
                schoolId = jwt.Claims.FirstOrDefault(c => c.Type == JwtCustomClaimNames.SchoolId)?.Value;
            }
            catch (Exception)
            {
                // Ignore malformed tokens here; authentication middleware validates later.
            }
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            tenantId = context.Request.Headers[HttpHeaderNames.TenantId].FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            tenantId = ResolveSubdomain(context.Request.Host.Host);
        }

        tenantContext.TenantId = tenantId;
        tenantContext.SchoolId = schoolId;

        await _next(context).ConfigureAwait(false);
    }

    private static string? ResolveSubdomain(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        string[] parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 3)
        {
            return parts[0];
        }

        return null;
    }
}
