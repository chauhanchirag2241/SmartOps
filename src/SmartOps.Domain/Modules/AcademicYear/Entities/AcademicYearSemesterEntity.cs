using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.AcademicYear.Entities;

public class AcademicYearSemesterEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public int SemesterIndex { get; set; }
    public string Name { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}
