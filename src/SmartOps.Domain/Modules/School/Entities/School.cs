using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.School.Entities;

public sealed class School : AuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Subdomain { get; set; } = string.Empty;
}
