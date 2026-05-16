namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class TenantContext
{
    public string? TenantId { get; set; }

    public string? SchoolId { get; set; }

    public string? SchemaName { get; set; }
}
