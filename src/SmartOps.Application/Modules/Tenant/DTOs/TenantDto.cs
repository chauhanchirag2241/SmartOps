namespace SmartOps.Application.Modules.Tenant.DTOs;

public sealed class TenantDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
