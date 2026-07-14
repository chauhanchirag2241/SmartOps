using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.AcademicYear.Entities;

[TrackHistory]
public class AcademicYearEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string Title { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }
    public bool IsCurrent { get; set; }
}
