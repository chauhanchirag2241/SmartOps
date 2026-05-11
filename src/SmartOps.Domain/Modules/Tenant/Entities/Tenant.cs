using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Tenant.Entities;

public sealed class Tenant : AuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Host subdomain segment used to resolve the tenant when not supplied via JWT or header.
    /// </summary>
    public string Subdomain { get; set; } = string.Empty;
}
