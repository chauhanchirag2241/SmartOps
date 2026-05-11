using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Identity.Entities;

public sealed class RolePermission : AuditableEntity
{
    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }
}
