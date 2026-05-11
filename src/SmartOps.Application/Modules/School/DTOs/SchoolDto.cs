namespace SmartOps.Application.Modules.School.DTOs;

public sealed class SchoolDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
