using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.FeeMaster.Entities;

[TrackHistory]
public sealed class FeeStudentAmountEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid FeeMasterId { get; set; }
    public Guid FeeHeadId { get; set; }
    public Guid StudentId { get; set; }
    /// <summary>Null for one-time/monthly heads; set for period-wise overrides.</summary>
    public Guid? AcademicPeriodId { get; set; }
    public decimal? Amount { get; set; }
    public bool IsExcluded { get; set; }
}
