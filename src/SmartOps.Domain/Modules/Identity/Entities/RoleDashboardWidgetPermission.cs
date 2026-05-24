using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Identity.Entities;

public sealed class RoleDashboardWidgetPermission : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid RoleId { get; set; }

    public Guid WidgetId { get; set; }

    public bool CanView { get; set; }
}
