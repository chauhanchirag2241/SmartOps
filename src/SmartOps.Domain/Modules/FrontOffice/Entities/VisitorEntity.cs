using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.FrontOffice.Entities;

[TrackHistory]
public sealed class VisitorEntity : AuditableEntity
{
    public Guid Id { get; set; }

    /// <summary>Set from active branch on write; not user-edited — omit from audit history.</summary>
    [TrackHistoryIgnore]
    public Guid BranchId { get; set; }

    public string Name { get; set; } = null!;
    public string? Phone { get; set; }
    public string? IdCardType { get; set; }
    public string? IdCardNumber { get; set; }
    public Guid PurposeId { get; set; }
    public string? MeetingWith { get; set; }
    public DateTime InTime { get; set; }
    public DateTime? OutTime { get; set; }
    public string? Note { get; set; }
    public string? DocumentPath { get; set; }
}
