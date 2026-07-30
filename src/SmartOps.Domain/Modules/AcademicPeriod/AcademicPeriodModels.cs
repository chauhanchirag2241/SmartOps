using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.AcademicPeriod;

public sealed class ClassAcademicPeriodEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ClassGroupId { get; set; }
    public int PeriodIndex { get; set; }
    public string Name { get; set; } = null!;
}

public sealed record AcademicPeriodClassSummary(
    Guid ClassId,
    string ClassName,
    int PeriodCount);
