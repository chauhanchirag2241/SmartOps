using SmartOps.Domain.Modules.Attendance;

namespace SmartOps.Application.Modules.Attendance;

public record GetAttendanceReportRequestDto(
    Guid ClassId,
    int Month,
    int Year
);

public record AttendanceReportStudentDto(
    Guid StudentId,
    string StudentName,
    string RollNo,
    string AvatarInitials,
    int TotalPresent,
    int TotalAbsent,
    int TotalLeave,
    int TotalLate,
    decimal AttendancePercentage,
    IDictionary<int, string> DailyStatus // Key: day of month (1-31), Value: "P", "A", "L", "late", "H", "S"
);

public record AttendanceReportResponseDto(
    Guid ClassId,
    string ClassName,
    int Month,
    int Year,
    int TotalWorkingDays,
    decimal ClassAveragePercentage,
    int StudentsWithPerfectAttendance,
    int StudentsBelow75Percent,
    int ChronicAbsentees,
    IList<AttendanceReportStudentDto> Students
);
