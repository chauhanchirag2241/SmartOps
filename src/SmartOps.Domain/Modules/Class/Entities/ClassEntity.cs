using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.Class.Entities;

[TrackHistory]
public class ClassEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ClassGroupId { get; set; }
    public int Section { get; set; }
    public int Capacity { get; set; }
    public string? RoomNumber { get; set; }
    public Guid? ShiftId { get; set; }

    /// <summary>From classgroups — not a columns on classes.</summary>
    [DbIgnore]
    public string ClassName { get; set; } = null!;

    [DbIgnore]
    public int? StreamGroup { get; set; }

    [DbIgnore]
    public Guid BranchId { get; set; }

    [DbIgnore]
    public int? Medium { get; set; }

    [DbIgnore]
    public string? Description { get; set; }
}
