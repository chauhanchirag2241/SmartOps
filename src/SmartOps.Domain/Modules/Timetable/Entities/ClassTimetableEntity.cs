using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.Timetable.Entities;

[TrackHistory]
public sealed class ClassTimetableEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid AcademicYearId { get; set; }
    public Guid ClassId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public string? Notes { get; set; }
}
