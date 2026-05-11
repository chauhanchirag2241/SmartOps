namespace SmartOps.Shared.Constants;

/// <summary>
/// Permission identifiers seeded and enforced by the authorization layer.
/// </summary>
public static class PermissionNames
{
    public const string StudentRead = "student.read";

    public const string StudentCreate = "student.create";

    public const string StudentUpdate = "student.update";

    public const string StudentDelete = "student.delete";

    public const string AttendanceRead = "attendance.read";

    public const string AttendanceMark = "attendance.mark";

    public const string FeesRead = "fees.read";

    public const string FeesCreate = "fees.create";

    public const string FeesUpdate = "fees.update";

    public const string ExamsRead = "exams.read";

    public const string ExamsCreate = "exams.create";

    public const string HrRead = "hr.read";

    public const string HrManage = "hr.manage";

    public const string ReportsView = "reports.view";

    public const string AdminFull = "admin.full";
}
