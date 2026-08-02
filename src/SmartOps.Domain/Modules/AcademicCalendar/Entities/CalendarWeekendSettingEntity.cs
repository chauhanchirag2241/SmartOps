using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.AcademicCalendar.Entities;

[TrackHistory]
public sealed class CalendarWeekendSettingEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public bool SundayOff { get; set; } = true;
    public bool SaturdayOff { get; set; }
    public bool MondayOff { get; set; }
    public bool TuesdayOff { get; set; }
    public bool WednesdayOff { get; set; }
    public bool ThursdayOff { get; set; }
    public bool FridayOff { get; set; }
}
