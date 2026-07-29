using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.Class.Entities;

[TrackHistory]
public class ClassGroupEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string ClassName { get; set; } = null!;
    public int? StreamGroup { get; set; }
    public int? Medium { get; set; }
    public string? Description { get; set; }
}
