namespace SmartOps.Application.Modules.School.DTOs;

public sealed class CreateSchoolDto
{
    public string Name { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;
}
