using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.AcademicPeriod;

public enum AcademicPeriodType : short
{
    Semester = 1,
    Term = 2,
    Quarter = 3,
    Custom = 4,
}

public sealed class ClassAcademicPeriodEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid AcademicYearId { get; set; }
    public AcademicPeriodType PeriodType { get; set; }
    public int PeriodIndex { get; set; }
    public string Name { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public sealed record AcademicPeriodClassSummary(
    Guid ClassId,
    string ClassName,
    Guid AcademicYearId,
    int PeriodCount,
    AcademicPeriodType? PeriodType);
