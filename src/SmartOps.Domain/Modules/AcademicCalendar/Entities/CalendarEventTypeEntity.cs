using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Attributes;

namespace SmartOps.Domain.Modules.AcademicCalendar.Entities;

[TrackHistory]
public sealed class CalendarEventTypeEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Color { get; set; } = "#5B8DEF";
    public bool IsNonWorkingDefault { get; set; }
    public int DisplayOrder { get; set; }
}
