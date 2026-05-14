using SmartOps.Domain.Modules.Attendance.Enums;

namespace SmartOps.Application.Modules.Attendance.DTOs;

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
