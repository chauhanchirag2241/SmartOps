namespace SmartOps.Domain.Modules.School;

public sealed class SchoolListModel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string SchoolCode { get; set; } = string.Empty;

    public string? PrimaryEmail { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? AffiliatedBoard { get; set; }

    public string Subdomain { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
