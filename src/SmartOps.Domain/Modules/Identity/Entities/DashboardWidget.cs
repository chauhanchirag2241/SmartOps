using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Identity.Entities;

public sealed class DashboardWidget : AuditableEntity
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string RequiredMenuCode { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public string DefaultSize { get; set; } = "stat";
}
