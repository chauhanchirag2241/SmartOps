using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Identity.Entities;

public sealed class Menu : AuditableEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public Guid? ParentMenuId { get; set; }

    public string? Route { get; set; }

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    public string Application { get; set; } = string.Empty;
}
