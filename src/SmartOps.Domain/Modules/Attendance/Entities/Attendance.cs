using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Attendance;

namespace SmartOps.Domain.Modules.Attendance.Entities;

public class Attendance : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public Guid StudentId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public AttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }
}
