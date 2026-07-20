using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.Timetable.Entities;

[TrackHistory]
public sealed class ClassTimetableSlotEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid TimetableId { get; set; }
    public int DayOfWeek { get; set; }
    public Guid PeriodId { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string? RoomNo { get; set; }
}
