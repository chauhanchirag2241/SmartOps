using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.School.Entities;

public sealed class SchoolEntity : AuditableEntity
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

    public string? SchemaName { get; set; }

    public string? DatabaseName { get; set; }

    public string? ConnectionString { get; set; }

    [DbIgnore]
    public List<SchoolBranchEntity> Branches { get; set; } = new();
}
