using SmartOps.Domain.Modules.School.Entities;

namespace SmartOps.Application.Modules.School;

public sealed class CreateSchoolDto
{
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string SchoolCode { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? AffiliatedBoard { get; set; }
    public string? SchoolType { get; set; }
    public int? EstablishedYear { get; set; }
    public string? AboutSchool { get; set; }
    public string? StreetAddress { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string Country { get; set; } = "India";
    public string? Timezone { get; set; }
    public string? GoogleMapsLink { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? PrimaryPhone { get; set; }
    public string? AlternatePhone { get; set; }
    public string? Fax { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? PrincipalEmail { get; set; }
    public string? Website { get; set; }
    public string? SchemaName { get; set; }

    public SchoolEntity ToEntity()
    {
        var entity = new SchoolEntity
        {
            Name = Name.Trim(),
            Subdomain = Subdomain.Trim().ToLowerInvariant(),
            SchoolCode = SchoolCode.Trim(),
            RegistrationNumber = RegistrationNumber,
            AffiliatedBoard = AffiliatedBoard,
            SchoolType = SchoolType,
            EstablishedYear = EstablishedYear,
            AboutSchool = AboutSchool,
            StreetAddress = StreetAddress,
            City = City,
            State = State,
            Pincode = Pincode,
            Country = string.IsNullOrWhiteSpace(Country) ? "India" : Country,
            Timezone = Timezone,
            GoogleMapsLink = GoogleMapsLink,
            Latitude = Latitude,
            Longitude = Longitude,
            PrimaryPhone = PrimaryPhone,
            AlternatePhone = AlternatePhone,
            Fax = Fax,
            PrimaryEmail = PrimaryEmail,
            PrincipalEmail = PrincipalEmail,
            Website = Website,
            SchemaName = SchemaName ?? $"school_{Subdomain.Trim().ToLowerInvariant().Replace('-', '_')}",
            IsActive = true,
            Branches =
            [
                new SchoolBranchEntity
                {
                    Name = $"{Name.Trim()} — Main Campus",
                    IsHeadOffice = true
                }
            ]
        };

        return entity;
    }
}

public sealed class UpdateSchoolDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Subdomain { get; set; } = string.Empty;
    public string SchoolCode { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? AffiliatedBoard { get; set; }
    public string? SchoolType { get; set; }
    public int? EstablishedYear { get; set; }
    public string? AboutSchool { get; set; }
    public string? StreetAddress { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string Country { get; set; } = "India";
    public string? Timezone { get; set; }
    public string? GoogleMapsLink { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? PrimaryPhone { get; set; }
    public string? AlternatePhone { get; set; }
    public string? Fax { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? PrincipalEmail { get; set; }
    public string? Website { get; set; }

    public void ApplyTo(SchoolEntity school)
    {
        school.Name = Name.Trim();
        school.Subdomain = Subdomain.Trim().ToLowerInvariant();
        school.SchoolCode = SchoolCode.Trim();
        school.RegistrationNumber = RegistrationNumber;
        school.AffiliatedBoard = AffiliatedBoard;
        school.SchoolType = SchoolType;
        school.EstablishedYear = EstablishedYear;
        school.AboutSchool = AboutSchool;
        school.StreetAddress = StreetAddress;
        school.City = City;
        school.State = State;
        school.Pincode = Pincode;
        school.Country = string.IsNullOrWhiteSpace(Country) ? "India" : Country;
        school.Timezone = Timezone;
        school.GoogleMapsLink = GoogleMapsLink;
        school.Latitude = Latitude;
        school.Longitude = Longitude;
        school.PrimaryPhone = PrimaryPhone;
        school.AlternatePhone = AlternatePhone;
        school.Fax = Fax;
        school.PrimaryEmail = PrimaryEmail;
        school.PrincipalEmail = PrincipalEmail;
        school.Website = Website;
    }
}

public sealed class SchoolBranchDto
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsHeadOffice { get; set; }
    public bool IsActive { get; set; }
}

public sealed class SaveSchoolBranchDto
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Address { get; set; }
}

public sealed class SchoolDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public sealed record CreateSchoolResponse(string Message, Guid SchoolId);

/// <summary>
/// Public school profile for tenant UI bootstrap (no sensitive config).
/// Branding uses hardcoded defaults — school table no longer stores theme columns.
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

public static class SchoolBootstrapMapping
{
    public static SchoolBootstrapDto ToBootstrapDto(this SchoolEntity school) =>
        new()
        {
            Id = school.Id,
            Name = school.Name,
            Subdomain = school.Subdomain,
            ShortName = null,
            Tagline = null,
            LogoUrl = null,
            FaviconUrl = null,
            PrimaryColor = "#639922",
            SecondaryColor = "#3b6d11",
            AccentColor = "#c0dd97",
            TextOnPrimary = "#ffffff",
            SchemaName = school.SchemaName ?? $"school_{school.Subdomain.Replace('-', '_')}",
        };

    public static SchoolBranchDto ToDto(this SchoolBranchEntity branch) =>
        new()
        {
            Id = branch.Id,
            SchoolId = branch.SchoolId,
            Name = branch.Name,
            Email = branch.Email,
            Address = branch.Address,
            IsHeadOffice = branch.IsHeadOffice,
            IsActive = branch.IsActive,
        };
}
