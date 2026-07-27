using SmartOps.Domain.Modules.StaffAttendance;

namespace SmartOps.Application.Modules.StaffAttendance;

public record StaffAttendanceRowDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeName,
    Guid? DepartmentId,
    string? DepartmentName,
    DateOnly AttendanceDate,
    DateTimeOffset? CheckInTime,
    DateTimeOffset? CheckOutTime,
    string? CheckInSource,
    string? CheckOutSource,
    StaffAttendanceStatus Status,
    string StatusLabel,
    string? Remarks,
    float? CheckInConfidence,
    float? CheckOutConfidence,
    bool IsFaceEnrolled,
    string? PhotoUrl,
    string? ShiftStartTime);

public record ManualPunchRequestDto(
    Guid? EmployeeId,
    string PunchType,
    DateOnly? AttendanceDate,
    DateTimeOffset? CheckInTime,
    DateTimeOffset? CheckOutTime,
    string? Remarks);

public record UpdateStaffAttendanceRequestDto(
    DateTimeOffset? CheckInTime,
    DateTimeOffset? CheckOutTime,
    StaffAttendanceStatus? Status,
    string? Remarks);

public record EmployeeAttendanceTypeSettingDto(
    string Type,
    bool AllowsManual,
    bool AllowsFace);

public record StaffAttendanceReportDto(
    int Month,
    int Year,
    Guid? DepartmentId,
    int TotalWorkingDays,
    IList<StaffAttendanceReportEmployeeDto> Employees);

public record StaffAttendanceReportEmployeeDto(
    Guid EmployeeId,
    string EmployeeName,
    string? DepartmentName,
    int PresentDays,
    int AbsentDays,
    int LateDays,
    int HalfDayDays,
    IDictionary<int, string> DailyStatus);

public record FaceEmbedResult(float[] Embedding, string Model);

public record FaceMatchCandidate(Guid EmployeeId, float[] Embedding);

public record FaceMatchResult(Guid EmployeeId, float Score);

public static class StaffAttendancePunchTypes
{
    public const string CheckIn = "checkin";
    public const string CheckOut = "checkout";

    public static bool IsValid(string? value) =>
        string.Equals(value, CheckIn, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, CheckOut, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? value) =>
        string.Equals(value, CheckOut, StringComparison.OrdinalIgnoreCase) ? CheckOut : CheckIn;
}

public static class StaffAttendanceStatusLabels
{
    public static string ToDisplayString(this StaffAttendanceStatus status) => status switch
    {
        StaffAttendanceStatus.Present => "Present",
        StaffAttendanceStatus.Absent => "Absent",
        StaffAttendanceStatus.Late => "Late",
        StaffAttendanceStatus.HalfDay => "Half Day",
        _ => status.ToString()
    };

    public static string ToReportCode(this StaffAttendanceStatus status) => status switch
    {
        StaffAttendanceStatus.Present => "P",
        StaffAttendanceStatus.Absent => "A",
        StaffAttendanceStatus.Late => "L",
        StaffAttendanceStatus.HalfDay => "H",
        _ => "?"
    };
}
