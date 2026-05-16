namespace SmartOps.Shared.Constants;

/// <summary>
/// Menu identifiers stored in global.menus.code and used for permission checks.
/// </summary>
public static class MenuCodes
{
    public const string Dashboard = "DASHBOARD";

    public const string Schools = "SCHOOLS";

    public const string Users = "USERS";

    public const string Roles = "ROLES";

    public const string Settings = "SETTINGS";

    public const string Academics = "ACADEMICS";

    public const string Students = "STUDENTS";

    public const string Teachers = "TEACHERS";

    public const string Classes = "CLASSES";

    public const string Subjects = "SUBJECTS";

    public const string AcademicYears = "ACADEMIC_YEARS";

    public const string Attendance = "ATTENDANCE";

    public static IReadOnlyList<string> All { get; } =
    [
        Dashboard,
        Schools,
        Users,
        Roles,
        Settings,
        Academics,
        Students,
        Teachers,
        Classes,
        Subjects,
        AcademicYears,
        Attendance
    ];
}
