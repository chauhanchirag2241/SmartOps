using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using SmartOps.Application.Abstractions;
using SmartOps.Domain.Modules.School.Entities;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class TenantResolverMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolverMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenantContext,
        ITenantSchoolResolver tenantSchoolResolver)
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

        if (!string.IsNullOrWhiteSpace(tenantId) &&
            (string.IsNullOrWhiteSpace(schoolId) || string.IsNullOrWhiteSpace(tenantContext.SchemaName)))
        {
            SchoolEntity? school = await tenantSchoolResolver
                .ResolveBySubdomainAsync(tenantId, context.RequestAborted)
                .ConfigureAwait(false);

            if (school is not null)
            {
                tenantContext.SchoolId = school.Id.ToString();
                tenantContext.DatabaseName = school.DatabaseName;
                tenantContext.ConnectionString = school.ConnectionString;

                if (!string.IsNullOrWhiteSpace(school.ConnectionString))
                {
                    tenantContext.SchemaName = SmartOps.Domain.Common.Configuration.DatabaseConfig.Schema_School;
                }
                else
                {
                    tenantContext.SchemaName = school.SchemaName
                        ?? $"school_{school.Subdomain.Replace('-', '_')}";
                }
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    private static string? ResolveSubdomain(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string[] parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 3)
        {
            string subdomain = parts[0];
            if (subdomain.Equals("www", StringComparison.OrdinalIgnoreCase) ||
                subdomain.Equals("admin", StringComparison.OrdinalIgnoreCase) ||
                subdomain.Equals("api", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return subdomain;
        }

        return null;
    }
}
