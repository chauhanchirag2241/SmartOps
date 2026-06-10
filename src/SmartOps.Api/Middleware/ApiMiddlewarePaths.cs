namespace SmartOps.Api.Middleware;

internal static class ApiMiddlewarePaths
{
    /// <summary>
    /// Paths that must not run tenant-scoped middleware (academic year, user scope) even when a JWT is present.
    /// </summary>
    internal static bool IsTenantContextBypass(HttpContext context)
    {
        string path = context.Request.Path.Value ?? string.Empty;

        return path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/schools/by-subdomain/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);
    }
}
