namespace SmartOps.Domain.Common.Constants;

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

    public const string ClassMappings = "CLASS_MAPPINGS";

    public const string Subjects = "SUBJECTS";

    public const string AcademicYears = "ACADEMIC_YEARS";

    public const string Attendance = "ATTENDANCE";

    public const string Homework = "HOMEWORK";

    public const string FeesStructure = "FEES_STRUCTURE";

    public const string FeesClassAmounts = "FEES_CLASS_AMOUNTS";

    public const string FeesCollection = "FEES_COLLECTION";

    public const string SalaryStructure = "SALARY_STRUCTURE";

    /// <summary>Legacy code; use <see cref="SalaryStructure"/>.</summary>
    public const string SalaryComponents = "SALARY_COMPONENTS";

    public const string SalaryEmployees = "SALARY_EMPLOYEES";

    public const string SalaryPayroll = "SALARY_PAYROLL";

    public const string LeaveStaff = "LEAVE_STAFF";

    public const string LeaveStudent = "LEAVE_STUDENT";

    public const string MyActions = "MY_ACTIONS";

    public const string Notices = "NOTICES";

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
        ClassMappings,
        Subjects,
        AcademicYears,
        Attendance,
        Homework,
        FeesStructure,
        FeesClassAmounts,
        FeesCollection,
        SalaryStructure,
        SalaryEmployees,
        SalaryPayroll,
        LeaveStaff,
        LeaveStudent,
        MyActions,
        Notices
    ];
}
