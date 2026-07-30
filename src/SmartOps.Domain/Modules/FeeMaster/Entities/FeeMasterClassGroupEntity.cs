using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.FeeMaster.Entities;

public sealed class FeeMasterClassGroupEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid FeeMasterId { get; set; }
    public Guid ClassGroupId { get; set; }
}
