using SmartOps.Domain.Modules.Attendance;
using AttendanceEntity = SmartOps.Domain.Modules.Attendance.Entities.Attendance;

namespace SmartOps.Application.Modules.Attendance;

public record SubmitAttendanceRequestDto(
    Guid ClassId,
    DateOnly AttendanceDate,
    IList<StudentAttendanceItemDto> Students
);

public record StudentAttendanceItemDto(
    Guid StudentId,
    AttendanceStatus Status,
    string? Remarks
);

public record AttendanceResponseDto(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string RollNo,
    AttendanceStatus Status,
    string StatusLabel,
    string? Remarks,
    DateOnly AttendanceDate
);

public record ClassAttendanceResponseDto(
    Guid ClassId,
    string ClassName,
    DateOnly AttendanceDate,
    int Total,
    int Present,
    int Absent,
    int Leave,
    int Late,
    bool IsSubmitted,
    IList<AttendanceResponseDto> Students
);

public record GetClassAttendanceRequestDto(
    Guid ClassId,
    DateOnly AttendanceDate
);

public record StudentAttendanceSummaryDto(
    Guid StudentId,
    string StudentName,
    int Month,
    int Year,
    int TotalDays,
    int PresentDays,
    int AbsentDays,
    int LeaveDays,
    int LateDays,
    decimal Percentage
);

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

