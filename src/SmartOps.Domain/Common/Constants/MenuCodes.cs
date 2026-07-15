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

    public const string AcademicSetup = "ACADEMIC_SETUP";

    public const string FeesManagement = "FEES_MANAGEMENT";

    public const string SalaryManagement = "SALARY_MANAGEMENT";

    public const string LeaveManagement = "LEAVE_MANAGEMENT";

    public const string Administration = "ADMINISTRATION";

    public const string Reports = "REPORTS";

    public const string Students = "STUDENTS";

    public const string Employees = "EMPLOYEES";

    [Obsolete("Use Employees instead.")]
    public const string Teachers = Employees;

    public const string Classes = "CLASSES";

    public const string ClassMappings = "CLASS_MAPPINGS";

    public const string Subjects = "SUBJECTS";

    public const string AcademicYears = "ACADEMIC_YEARS";

    public const string Attendance = "ATTENDANCE";

    public const string AttendanceReport = "ATTENDANCE_REPORT";

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

    public const string FrontOffice = "FRONT_OFFICE";

    public const string VisitorBook = "VISITOR_BOOK";

    public const string PhoneLogs = "PHONE_LOGS";

    public const string Complaints = "COMPLAINTS";

    public const string AdmissionInquiries = "ADMISSION_INQUIRIES";

    public const string FrontOfficeSetup = "FRONT_OFFICE_SETUP";

    public static IReadOnlyList<string> All { get; } =
    [
        Dashboard,
        Schools,
        Users,
        Roles,
        Settings,
        Academics,
        AcademicSetup,
        FeesManagement,
        SalaryManagement,
        LeaveManagement,
        Administration,
        Reports,
        Students,
        Employees,
        Classes,
        ClassMappings,
        Subjects,
        AcademicYears,
        Attendance,
        AttendanceReport,
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
        Notices,
        FrontOffice,
        VisitorBook,
        PhoneLogs,
        Complaints,
        AdmissionInquiries,
        FrontOfficeSetup
    ];
}
