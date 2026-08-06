using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.StaffAttendance.Entities;

public class StaffAttendanceEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? CheckInSource { get; set; }
    public string? CheckOutSource { get; set; }
    public StaffAttendanceStatus Status { get; set; } = StaffAttendanceStatus.Present;
    public string? Remarks { get; set; }
    public float? CheckInConfidence { get; set; }
    public float? CheckOutConfidence { get; set; }
    public Guid MarkedByUserId { get; set; }
}
