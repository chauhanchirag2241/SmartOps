namespace SmartOps.Domain.Modules.StaffAttendance;

public enum StaffAttendanceStatus : short
{
    Present = 1,
    Absent = 2,
    Late = 3,
    HalfDay = 4,
}

public static class StaffAttendanceSources
{
    public const string Manual = "manual";
    public const string Face = "face";

    public static bool IsValid(string? value) =>
        string.Equals(value, Manual, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Face, StringComparison.OrdinalIgnoreCase);
}
