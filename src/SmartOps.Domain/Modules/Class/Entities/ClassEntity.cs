using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.Class.Entities;

[TrackHistory]
public class ClassEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ClassGroupId { get; set; }
    public string Section { get; set; } = null!;
    public int Capacity { get; set; }
    public string? RoomNumber { get; set; }
    public Guid? ShiftId { get; set; }

    /// <summary>From classgroups — not a column on classes.</summary>
    [DbIgnore]
    public string ClassName { get; set; } = null!;

    [DbIgnore]
    public Guid BranchId { get; set; }

    [DbIgnore]
    public string? Description { get; set; }
}
