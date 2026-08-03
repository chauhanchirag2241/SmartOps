using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.AcademicCalendar.Entities;

[TrackHistory]
public sealed class CalendarEventEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid EventTypeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool AppliesToStudents { get; set; } = true;
    public bool AppliesToTeachers { get; set; } = true;
    public bool AppliesToStaff { get; set; } = true;
    public bool IsNonWorkingDay { get; set; }
    public string? Color { get; set; }
    /// <summary>When set, this calendar entry is auto-synced from an exam schedule.</summary>
    public Guid? SourceExamId { get; set; }
}
