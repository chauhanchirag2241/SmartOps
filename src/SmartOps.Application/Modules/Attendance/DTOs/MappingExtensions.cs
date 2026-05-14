using SmartOps.Domain.Modules.Attendance.Enums;
using AttendanceEntity = SmartOps.Domain.Modules.Attendance.Entities.Attendance;

namespace SmartOps.Application.Modules.Attendance.DTOs;

public static class AttendanceMappingExtensions
{
    public static AttendanceResponseDto ToDto(
        this AttendanceEntity a,
        string studentName = "",
        string rollNo = "") => new(
            a.Id,
            a.StudentId,
            studentName,
            rollNo,
            a.Status,
            a.Status.ToDisplayString(),
            a.Remarks,
            a.AttendanceDate
        );
}
