using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.FeeMaster.Entities;

[TrackHistory]
public sealed class FeeHeadPeriodAmountEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid FeeHeadId { get; set; }
    public Guid ClassGroupId { get; set; }
    public Guid AcademicPeriodId { get; set; }
    public decimal Amount { get; set; }
}
