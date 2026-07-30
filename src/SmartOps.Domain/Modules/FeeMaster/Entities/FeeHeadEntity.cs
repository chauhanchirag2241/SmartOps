using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.FeeMaster.Entities;

[TrackHistory]
public sealed class FeeHeadEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid FeeMasterId { get; set; }
    public string FeeHeadName { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;
    public bool IsEditable { get; set; }
    public decimal? Amount { get; set; }
    public string? ApplicableMonths { get; set; }
}
