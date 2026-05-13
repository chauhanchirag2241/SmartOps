using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.AcademicYear.Entities;

public class AcademicYearEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }
}
