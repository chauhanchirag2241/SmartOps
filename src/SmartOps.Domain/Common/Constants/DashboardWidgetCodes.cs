namespace SmartOps.Domain.Common.Constants;

/// <summary>
/// Dashboard widget identifiers stored in global.dashboard_widgets.code.
/// </summary>
public static class DashboardWidgetCodes
{
    public const string StudentsStat = "STUDENTS_STAT";
    public const string EmployeesStat = "EMPLOYEES_STAT";
    public const string ClassesStat = "CLASSES_STAT";
    public const string SubjectsStat = "SUBJECTS_STAT";
    public const string SalaryDisbursed = "SALARY_DISBURSED";
    public const string AttendanceRate = "ATTENDANCE_RATE";
    public const string AttendanceDetail = "ATTENDANCE_DETAIL";
    public const string SalaryStatus = "SALARY_STATUS";
    public const string RecentStudents = "RECENT_STUDENTS";
    public const string EmployeesList = "EMPLOYEES_LIST";

    [Obsolete("Use EmployeesStat instead.")]
    public const string TeachersStat = EmployeesStat;

    [Obsolete("Use EmployeesList instead.")]
    public const string TeachersList = EmployeesList;
    public const string HomeworkDue = "HOMEWORK_DUE";
    public const string ClassesOverview = "CLASSES_OVERVIEW";
    public const string AlertsActions = "ALERTS_ACTIONS";

    public static IReadOnlyList<string> All { get; } =
    [
        StudentsStat,
        EmployeesStat,
        ClassesStat,
        SubjectsStat,
        SalaryDisbursed,
        AttendanceRate,
        AttendanceDetail,
        SalaryStatus,
        RecentStudents,
        EmployeesList,
        HomeworkDue,
        ClassesOverview,
        AlertsActions
    ];
}
