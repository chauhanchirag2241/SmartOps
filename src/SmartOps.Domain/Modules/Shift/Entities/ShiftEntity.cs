using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.Shift.Entities;

[TrackHistory]
public sealed class ShiftEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
