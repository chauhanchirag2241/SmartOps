namespace SmartOps.Domain.Modules.Attendance;

public enum AttendanceStatus
{
    Present = 1,
    Absent = 2,
    Late = 3
}

public static class AttendanceStatusExtensions
{
    public static string ToDisplayString(this AttendanceStatus status) =>
        status switch
        {
            AttendanceStatus.Present => "Present",
            AttendanceStatus.Absent => "Absent",
            AttendanceStatus.Late => "Late",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

    public static bool IsValid(int value) =>
        Enum.IsDefined(typeof(AttendanceStatus), value);
}
