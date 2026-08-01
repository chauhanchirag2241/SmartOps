using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.AcademicPeriod;

[TrackHistory]
public sealed class ClassAcademicPeriodEntity : AuditableEntity
{
    public Guid Id { get; set; }

    [TrackHistoryIgnore]
    public Guid ClassGroupId { get; set; }

    public int PeriodIndex { get; set; }
    public string Name { get; set; } = null!;
}
