namespace SmartOps.Infrastructure.MultiTenancy;

public sealed class TenantContext
{
    public string? TenantId { get; set; }

    public string? SchoolId { get; set; }

    public string? SchemaName { get; set; }

    public string? DatabaseName { get; set; }

    public string? ConnectionString { get; set; }

    public bool UsesDedicatedDatabase => !string.IsNullOrWhiteSpace(ConnectionString);
}
