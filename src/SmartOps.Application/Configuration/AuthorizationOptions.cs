namespace SmartOps.Application.Configuration;

public sealed class AuthorizationOptions
{
    public const string SectionName = "Authorization";

    public bool EnableDataScopes { get; set; } = true;

    public int ScopeCacheMinutes { get; set; } = 5;
}
