using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.FrontOffice.Entities;

public sealed class ComplaintTypeEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int DisplayOrder { get; set; }
}
