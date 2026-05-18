namespace SmartOps.Shared.Constants;

/// <summary>
/// Custom JWT claim names (in addition to standard <see cref="System.Security.Claims.ClaimTypes"/>).
/// </summary>
public static class JwtCustomClaimNames
{
    public const string TenantId = "tenantid";

    public const string SchoolId = "schoolid";

    public const string Permissions = "permissions";

    public const string ScopeVersion = "scope_ver";

    public const string ScopeType = "scope_type";
}
