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

    /// <summary>Synced with Status == Current for existing consumers.</summary>
    public bool IsCurrent { get; set; }

    public AcademicYearStatus Status { get; set; } = AcademicYearStatus.Draft;
}
