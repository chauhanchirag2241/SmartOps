using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Identity.Entities;

public sealed class RoleMenuPermission : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid RoleId { get; set; }

    public Guid MenuId { get; set; }

    public bool CanView { get; set; }

    public bool CanAdd { get; set; }

    public bool CanEdit { get; set; }

    public bool CanDelete { get; set; }

    public bool CanExport { get; set; }
}
