using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Identity.Entities;

public sealed class UserSchoolMapping : AuditableEntity
{
    public Guid UserId { get; set; }

    public Guid SchoolId { get; set; }

    /// <summary>
    /// School-scoped role label (e.g. teacher, admin) stored with the mapping.
    /// </summary>
    public string Role { get; set; } = string.Empty;
}
