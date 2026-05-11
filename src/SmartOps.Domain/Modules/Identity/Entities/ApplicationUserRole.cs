using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Identity.Entities;

public sealed class ApplicationUserRole : AuditableEntity
{
    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }
}
