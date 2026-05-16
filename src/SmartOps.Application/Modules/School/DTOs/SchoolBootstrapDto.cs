namespace SmartOps.Application.Modules.School.DTOs;

/// <summary>
/// Public school profile for tenant UI bootstrap (no sensitive config).
/// </summary>
public sealed class SchoolBootstrapDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    public string? Tagline { get; set; }

    public string? LogoUrl { get; set; }

    public string? FaviconUrl { get; set; }

    public string PrimaryColor { get; set; } = "#639922";

    public string? SecondaryColor { get; set; }

    public string? AccentColor { get; set; }

    public string? TextOnPrimary { get; set; }

    public string SchemaName { get; set; } = string.Empty;
}
