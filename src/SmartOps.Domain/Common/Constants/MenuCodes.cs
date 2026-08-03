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

    public const string SalaryManagement = "SALARY_MANAGEMENT";

    public const string LeaveManagement = "LEAVE_MANAGEMENT";

    public const string Administration = "ADMINISTRATION";

    public const string Reports = "REPORTS";

    public const string Students = "STUDENTS";

    public const string Employees = "EMPLOYEES";

    public const string Teachers = "TEACHERS";

    public const string Classes = "CLASSES";

    public const string Shifts = "SHIFTS";

    public const string AcademicCalendar = "ACADEMIC_CALENDAR";

    public const string PromoteStudents = "PROMOTE_STUDENTS";

    public const string RollNumbers = "ROLL_NUMBERS";

    public const string AcademicYears = "ACADEMIC_YEARS";

    public const string Attendance = "ATTENDANCE";

    public const string AttendanceReport = "ATTENDANCE_REPORT";

    public const string StaffAttendance = "STAFF_ATTENDANCE";

    public const string StaffAttendanceReport = "STAFF_ATTENDANCE_REPORT";

    public const string Homework = "HOMEWORK";

    public const string SalaryStructure = "SALARY_STRUCTURE";

    /// <summary>Legacy code; use <see cref="SalaryStructure"/>.</summary>
    public const string SalaryComponents = "SALARY_COMPONENTS";

    public const string SalaryEmployees = "SALARY_EMPLOYEES";

    public const string SalaryPayroll = "SALARY_PAYROLL";

    public const string LeaveStaff = "LEAVE_STAFF";

    public const string LeaveStudent = "LEAVE_STUDENT";

    public const string LeaveTypes = "LEAVE_TYPES";

    public const string LeavePolicies = "LEAVE_POLICIES";

    public const string LeaveBalances = "LEAVE_BALANCES";

    public const string JobMaster = "JOB_MASTER";

    public const string MyActions = "MY_ACTIONS";

    public const string Notices = "NOTICES";

    public const string FrontOffice = "FRONT_OFFICE";

    public const string VisitorBook = "VISITOR_BOOK";

    public const string PhoneLogs = "PHONE_LOGS";

    public const string Complaints = "COMPLAINTS";

    public const string AdmissionInquiries = "ADMISSION_INQUIRIES";

    public const string FrontOfficeSetup = "FRONT_OFFICE_SETUP";

    public const string ExamManagement = "EXAM_MANAGEMENT";

    public const string ExamGroups = "EXAM_GROUPS";

    public const string Exams = "EXAMS";

    public const string ExamSchedule = "EXAM_SCHEDULE";

    public const string ExamMarksEntry = "EXAM_MARKS_ENTRY";

    public const string ExamResults = "EXAM_RESULTS";

    public const string ExamHallTickets = "EXAM_HALL_TICKETS";

    public const string ExamGradeSetup = "EXAM_GRADE_SETUP";

    public const string Timetable = "TIMETABLE";

    public const string PeriodMaster = "PERIOD_MASTER";

    public const string ClassTimetable = "CLASS_TIMETABLE";

    public const string MyTimetable = "MY_TIMETABLE";

    public const string TeacherTimetableReport = "TEACHER_TIMETABLE_REPORT";

    public const string FeeManagement = "FEE_MANAGEMENT";

    public const string FeeMaster = "FEE_MASTER";

    public const string FeeCollection = "FEE_COLLECTION";

    public const string BulkImport = "BULK_IMPORT";

    public const string StudentBulkImport = "STUDENT_BULK_IMPORT";

    public static IReadOnlyList<string> All { get; } =
    [
        Dashboard,
        Schools,
        Users,
        Roles,
        Settings,
        Academics,
        AcademicSetup,
        SalaryManagement,
        LeaveManagement,
        Administration,
        Reports,
        Students,
        Employees,
        Teachers,
        Classes,
        Shifts,
        AcademicCalendar,
        PromoteStudents,
        RollNumbers,
        AcademicYears,
        Attendance,
        AttendanceReport,
        StaffAttendance,
        StaffAttendanceReport,
        Homework,
        SalaryStructure,
        SalaryEmployees,
        SalaryPayroll,
        LeaveStaff,
        LeaveStudent,
        LeaveTypes,
        LeavePolicies,
        LeaveBalances,
        JobMaster,
        MyActions,
        Notices,
        FrontOffice,
        VisitorBook,
        PhoneLogs,
        Complaints,
        AdmissionInquiries,
        FrontOfficeSetup,
        ExamManagement,
        ExamGradeSetup,
        ExamGroups,
        Exams,
        ExamSchedule,
        ExamHallTickets,
        ExamMarksEntry,
        ExamResults,
        Timetable,
        PeriodMaster,
        ClassTimetable,
        MyTimetable,
        TeacherTimetableReport,
        FeeManagement,
        FeeMaster,
        FeeCollection,
        BulkImport,
        StudentBulkImport
    ];
}
