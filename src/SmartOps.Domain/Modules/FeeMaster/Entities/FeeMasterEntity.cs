using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.FeeMaster.Entities;

[TrackHistory]
public sealed class FeeMasterEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public string FeeName { get; set; } = string.Empty;
    public string FeeType { get; set; } = string.Empty;
    public DateOnly? PublishedOn { get; set; }
    public DateOnly? DefaultDueDate { get; set; }
    public string ApplicableTo { get; set; } = string.Empty;
    public string? Description { get; set; }
}
