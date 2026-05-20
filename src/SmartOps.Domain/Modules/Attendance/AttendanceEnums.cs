namespace SmartOps.Domain.Modules.Attendance;

public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    Leave = 3,
    Late = 4
}

public static class AttendanceStatusExtensions
{
    public static string ToDisplayString(this AttendanceStatus status) =>
        status switch
        {
            AttendanceStatus.Present => "Present",
            AttendanceStatus.Absent => "Absent",
            AttendanceStatus.Leave => "Leave",
            AttendanceStatus.Late => "Late",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

    public static bool IsValid(int value) =>
        Enum.IsDefined(typeof(AttendanceStatus), value);
}
