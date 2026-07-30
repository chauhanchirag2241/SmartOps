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

    /// <summary>Legacy column retained for schema compatibility; current year is date-derived.</summary>
    public AcademicYearStatus Status { get; set; } = AcademicYearStatus.Draft;

    /// <summary>True when today falls between StartDate and EndDate (inclusive).</summary>
    [DbIgnore]
    [TrackHistoryIgnore]
    public bool IsCurrent
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return IsActive && today >= StartDate && today <= EndDate;
        }
    }
}
