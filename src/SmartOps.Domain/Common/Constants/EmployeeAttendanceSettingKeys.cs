namespace SmartOps.Domain.Common.Constants;

public static class EmployeeAttendanceSettingKeys
{
    public const string Prefix = "attendance.";

    /// <summary>Manual | Face | Both</summary>
    public const string EmployeeType = "attendance.employee.type";

    /// <summary>
    /// Default full working day length in hours when employee has no shift start+end.
    /// Half-day threshold is half of this value.
    /// </summary>
    public const string DefaultWorkingHours = "attendance.employee.defaultWorkingHours";

    public const string DefaultWorkingHoursValue = "8";

    public static readonly string[] AllKeys = [EmployeeType, DefaultWorkingHours];
}

public static class EmployeeAttendanceTypes
{
    public const string Manual = "Manual";
    public const string Face = "Face";
    public const string Both = "Both";

    public static bool IsValid(string? value) =>
        string.Equals(value, Manual, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Face, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Both, StringComparison.OrdinalIgnoreCase);

    public static bool AllowsManual(string? value)
    {
        string normalized = Normalize(value);
        return normalized is Manual or Both;
    }

    public static bool AllowsFace(string? value)
    {
        string normalized = Normalize(value);
        return normalized is Face or Both;
    }

    public static string Normalize(string? value)
    {
        if (string.Equals(value, Manual, StringComparison.OrdinalIgnoreCase))
        {
            return Manual;
        }

        if (string.Equals(value, Face, StringComparison.OrdinalIgnoreCase))
        {
            return Face;
        }

        if (string.Equals(value, Both, StringComparison.OrdinalIgnoreCase))
        {
            return Both;
        }

        return Both;
    }
}
