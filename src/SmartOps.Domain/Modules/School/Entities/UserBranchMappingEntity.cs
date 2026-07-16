using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.School.Entities;

public sealed class UserBranchMappingEntity : AuditableEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid BranchId { get; set; }

    public Guid SchoolId { get; set; }

    public bool IsDefault { get; set; }
}
