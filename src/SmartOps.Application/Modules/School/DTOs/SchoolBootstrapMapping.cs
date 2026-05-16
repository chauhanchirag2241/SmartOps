using SmartOps.Domain.Modules.School.Entities;

namespace SmartOps.Application.Modules.School.DTOs;

public static class SchoolBootstrapMapping
{
    public static SchoolBootstrapDto ToBootstrapDto(this SchoolEntity school) =>
        new()
        {
            Id = school.Id,
            Name = school.Name,
            Subdomain = school.Subdomain,
            ShortName = school.ShortName,
            Tagline = school.Tagline,
            LogoUrl = school.LogoUrl,
            FaviconUrl = school.FaviconUrl,
            PrimaryColor = school.PrimaryColor,
            SecondaryColor = school.SecondaryColor,
            AccentColor = school.AccentColor,
            TextOnPrimary = school.TextOnPrimary,
            SchemaName = school.SchemaName ?? $"school_{school.Subdomain.Replace('-', '_')}",
        };
}
