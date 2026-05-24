namespace SmartOps.Domain.Common.Constants;

/// <summary>
/// Dashboard widget identifiers stored in global.dashboard_widgets.code.
/// </summary>
public static class DashboardWidgetCodes
{
    public const string StudentsStat = "STUDENTS_STAT";
    public const string TeachersStat = "TEACHERS_STAT";
    public const string ClassesStat = "CLASSES_STAT";
    public const string SubjectsStat = "SUBJECTS_STAT";
    public const string FeesCollected = "FEES_COLLECTED";
    public const string FeesPending = "FEES_PENDING";
    public const string SalaryDisbursed = "SALARY_DISBURSED";
    public const string AttendanceRate = "ATTENDANCE_RATE";
    public const string AttendanceDetail = "ATTENDANCE_DETAIL";
    public const string FeesByClass = "FEES_BY_CLASS";
    public const string SalaryStatus = "SALARY_STATUS";
    public const string RecentStudents = "RECENT_STUDENTS";
    public const string TeachersList = "TEACHERS_LIST";
    public const string HomeworkDue = "HOMEWORK_DUE";
    public const string ClassesOverview = "CLASSES_OVERVIEW";
    public const string AlertsActions = "ALERTS_ACTIONS";

    public static IReadOnlyList<string> All { get; } =
    [
        StudentsStat,
        TeachersStat,
        ClassesStat,
        SubjectsStat,
        FeesCollected,
        FeesPending,
        SalaryDisbursed,
        AttendanceRate,
        AttendanceDetail,
        FeesByClass,
        SalaryStatus,
        RecentStudents,
        TeachersList,
        HomeworkDue,
        ClassesOverview,
        AlertsActions
    ];
}
