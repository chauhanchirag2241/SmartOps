namespace SmartOps.Domain.Common.Constants;

/// <summary>
/// Default menu grants for seeded school roles.
/// Menu IDs match global menu seed GUIDs (<c>10000000-…</c>).
/// </summary>
public static class DefaultSchoolRolePermissions
{
    public readonly record struct Grant(
        Guid MenuId,
        string MenuCode,
        bool CanView,
        bool CanAdd,
        bool CanEdit,
        bool CanDelete,
        bool CanExport);

    // Stable menu IDs from G010+ seed migrations
    private static readonly Guid Dashboard = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Academics = Guid.Parse("10000000-0000-0000-0000-000000000010");
    private static readonly Guid Students = Guid.Parse("10000000-0000-0000-0000-000000000011");
    private static readonly Guid Employees = Guid.Parse("10000000-0000-0000-0000-000000000012");
    private static readonly Guid Classes = Guid.Parse("10000000-0000-0000-0000-000000000013");
    private static readonly Guid AcademicYears = Guid.Parse("10000000-0000-0000-0000-000000000015");
    private static readonly Guid Attendance = Guid.Parse("10000000-0000-0000-0000-000000000016");
    private static readonly Guid Homework = Guid.Parse("10000000-0000-0000-0000-000000000018");
    private static readonly Guid AttendanceReport = Guid.Parse("10000000-0000-0000-0000-000000000019");
    private static readonly Guid SalaryStructure = Guid.Parse("10000000-0000-0000-0000-000000000023");
    private static readonly Guid SalaryEmployees = Guid.Parse("10000000-0000-0000-0000-000000000024");
    private static readonly Guid SalaryPayroll = Guid.Parse("10000000-0000-0000-0000-000000000025");
    private static readonly Guid LeaveStaff = Guid.Parse("10000000-0000-0000-0000-000000000026");
    private static readonly Guid LeaveStudent = Guid.Parse("10000000-0000-0000-0000-000000000027");
    private static readonly Guid MyActions = Guid.Parse("10000000-0000-0000-0000-000000000028");
    private static readonly Guid Notices = Guid.Parse("10000000-0000-0000-0000-000000000029");
    private static readonly Guid SchoolUsers = Guid.Parse("10000000-0000-0000-0000-000000000030");
    private static readonly Guid SchoolRoles = Guid.Parse("10000000-0000-0000-0000-000000000031");
    private static readonly Guid SchoolSettings = Guid.Parse("10000000-0000-0000-0000-000000000032");
    private static readonly Guid SalaryManagement = Guid.Parse("10000000-0000-0000-0000-000000000041");
    private static readonly Guid LeaveManagement = Guid.Parse("10000000-0000-0000-0000-000000000042");
    private static readonly Guid Administration = Guid.Parse("10000000-0000-0000-0000-000000000043");
    private static readonly Guid Reports = Guid.Parse("10000000-0000-0000-0000-000000000044");
    private static readonly Guid AcademicSetup = Guid.Parse("10000000-0000-0000-0000-000000000045");
    private static readonly Guid Shifts = Guid.Parse("10000000-0000-0000-0000-000000000047");
    private static readonly Guid AcademicCalendar = Guid.Parse("10000000-0000-0000-0000-000000000083");
    private static readonly Guid PromoteStudents = Guid.Parse("10000000-0000-0000-0000-000000000048");
    private static readonly Guid RollNumbers = Guid.Parse("10000000-0000-0000-0000-000000000049");
    private static readonly Guid FrontOffice = Guid.Parse("10000000-0000-0000-0000-000000000050");
    private static readonly Guid VisitorBook = Guid.Parse("10000000-0000-0000-0000-000000000051");
    private static readonly Guid PhoneLogs = Guid.Parse("10000000-0000-0000-0000-000000000052");
    private static readonly Guid Complaints = Guid.Parse("10000000-0000-0000-0000-000000000053");
    private static readonly Guid AdmissionInquiries = Guid.Parse("10000000-0000-0000-0000-000000000054");
    private static readonly Guid FrontOfficeSetup = Guid.Parse("10000000-0000-0000-0000-000000000055");
    private static readonly Guid ExamManagement = Guid.Parse("10000000-0000-0000-0000-000000000060");
    private static readonly Guid ExamGroups = Guid.Parse("10000000-0000-0000-0000-000000000061");
    private static readonly Guid Exams = Guid.Parse("10000000-0000-0000-0000-000000000062");
    private static readonly Guid ExamSchedule = Guid.Parse("10000000-0000-0000-0000-000000000063");
    private static readonly Guid ExamMarksEntry = Guid.Parse("10000000-0000-0000-0000-000000000064");
    private static readonly Guid ExamResults = Guid.Parse("10000000-0000-0000-0000-000000000065");
    private static readonly Guid ExamHallTickets = Guid.Parse("10000000-0000-0000-0000-000000000066");
    private static readonly Guid ExamGradeSetup = Guid.Parse("10000000-0000-0000-0000-000000000067");
    private static readonly Guid Timetable = Guid.Parse("10000000-0000-0000-0000-000000000070");
    private static readonly Guid PeriodMaster = Guid.Parse("10000000-0000-0000-0000-000000000071");
    private static readonly Guid ClassTimetable = Guid.Parse("10000000-0000-0000-0000-000000000072");
    private static readonly Guid MyTimetable = Guid.Parse("10000000-0000-0000-0000-000000000073");
    private static readonly Guid TeacherTimetableReport = Guid.Parse("10000000-0000-0000-0000-000000000074");
    private static readonly Guid StaffAttendance = Guid.Parse("10000000-0000-0000-0000-000000000075");
    private static readonly Guid StaffAttendanceReport = Guid.Parse("10000000-0000-0000-0000-000000000076");
    private static readonly Guid FeeManagement = Guid.Parse("10000000-0000-0000-0000-000000000080");
    private static readonly Guid FeeMaster = Guid.Parse("10000000-0000-0000-0000-000000000081");
    private static readonly Guid FeeCollection = Guid.Parse("10000000-0000-0000-0000-000000000082");

    private static Grant V(Guid id, string code) => new(id, code, true, false, false, false, false);
    private static Grant VA(Guid id, string code) => new(id, code, true, true, false, false, false);
    private static Grant VAE(Guid id, string code) => new(id, code, true, true, true, false, false);
    private static Grant Full(Guid id, string code) => new(id, code, true, true, true, true, true);
    private static Grant ViewExport(Guid id, string code) => new(id, code, true, false, false, false, true);

    /// <summary>Grants for roles other than Admin (Admin keeps all-menu seed).</summary>
    public static IReadOnlyDictionary<string, Grant[]> ByRoleName { get; } =
        new Dictionary<string, Grant[]>(StringComparer.OrdinalIgnoreCase)
        {
            [RoleNames.Teacher] = TeacherGrants(),
            [RoleNames.Principal] = PrincipalGrants(),
            [RoleNames.Accountant] = AccountantGrants(),
            [RoleNames.FrontOfficeExecutive] = FrontOfficeExecutiveGrants(),
        };

    private static Grant[] TeacherGrants() =>
    [
        V(Dashboard, MenuCodes.Dashboard),
        V(Academics, MenuCodes.Academics),
        ViewExport(Students, MenuCodes.Students),
        Full(Attendance, MenuCodes.Attendance),
        ViewExport(AttendanceReport, MenuCodes.AttendanceReport),
        Full(Homework, MenuCodes.Homework),
        V(LeaveManagement, MenuCodes.LeaveManagement),
        VA(LeaveStaff, MenuCodes.LeaveStaff),
        VAE(LeaveStudent, MenuCodes.LeaveStudent),
        VAE(MyActions, MenuCodes.MyActions),
        VAE(Notices, MenuCodes.Notices),
        V(ExamManagement, MenuCodes.ExamManagement),
        V(Exams, MenuCodes.Exams),
        V(ExamSchedule, MenuCodes.ExamSchedule),
        Full(ExamMarksEntry, MenuCodes.ExamMarksEntry),
        V(ExamResults, MenuCodes.ExamResults),
        V(ExamHallTickets, MenuCodes.ExamHallTickets),
        V(Timetable, MenuCodes.Timetable),
        V(ClassTimetable, MenuCodes.ClassTimetable),
        V(MyTimetable, MenuCodes.MyTimetable),
        ViewExport(TeacherTimetableReport, MenuCodes.TeacherTimetableReport),
        V(Reports, MenuCodes.Reports),
    ];

    private static Grant[] PrincipalGrants() =>
    [
        V(Dashboard, MenuCodes.Dashboard),
        V(Academics, MenuCodes.Academics),
        Full(Students, MenuCodes.Students),
        Full(Attendance, MenuCodes.Attendance),
        Full(AttendanceReport, MenuCodes.AttendanceReport),
        Full(Homework, MenuCodes.Homework),
        V(AcademicSetup, MenuCodes.AcademicSetup),
        Full(AcademicYears, MenuCodes.AcademicYears),
        Full(Classes, MenuCodes.Classes),
        Full(Shifts, MenuCodes.Shifts),
        Full(AcademicCalendar, MenuCodes.AcademicCalendar),
        Full(PromoteStudents, MenuCodes.PromoteStudents),
        Full(RollNumbers, MenuCodes.RollNumbers),
        V(LeaveManagement, MenuCodes.LeaveManagement),
        Full(LeaveStaff, MenuCodes.LeaveStaff),
        Full(LeaveStudent, MenuCodes.LeaveStudent),
        Full(MyActions, MenuCodes.MyActions),
        V(Administration, MenuCodes.Administration),
        Full(Employees, MenuCodes.Employees),
        Full(Notices, MenuCodes.Notices),
        Full(StaffAttendance, MenuCodes.StaffAttendance),
        ViewExport(StaffAttendanceReport, MenuCodes.StaffAttendanceReport),
        VAE(SchoolUsers, MenuCodes.Users),
        V(SchoolRoles, MenuCodes.Roles),
        VAE(SchoolSettings, MenuCodes.Settings),
        V(ExamManagement, MenuCodes.ExamManagement),
        Full(ExamGradeSetup, MenuCodes.ExamGradeSetup),
        Full(ExamGroups, MenuCodes.ExamGroups),
        Full(Exams, MenuCodes.Exams),
        Full(ExamSchedule, MenuCodes.ExamSchedule),
        Full(ExamMarksEntry, MenuCodes.ExamMarksEntry),
        Full(ExamResults, MenuCodes.ExamResults),
        Full(ExamHallTickets, MenuCodes.ExamHallTickets),
        V(Timetable, MenuCodes.Timetable),
        Full(PeriodMaster, MenuCodes.PeriodMaster),
        Full(ClassTimetable, MenuCodes.ClassTimetable),
        V(MyTimetable, MenuCodes.MyTimetable),
        ViewExport(TeacherTimetableReport, MenuCodes.TeacherTimetableReport),
        V(FrontOffice, MenuCodes.FrontOffice),
        V(VisitorBook, MenuCodes.VisitorBook),
        V(PhoneLogs, MenuCodes.PhoneLogs),
        V(Complaints, MenuCodes.Complaints),
        V(AdmissionInquiries, MenuCodes.AdmissionInquiries),
        V(FeeManagement, MenuCodes.FeeManagement),
        V(FeeMaster, MenuCodes.FeeMaster),
        V(FeeCollection, MenuCodes.FeeCollection),
        V(SalaryManagement, MenuCodes.SalaryManagement),
        V(SalaryStructure, MenuCodes.SalaryStructure),
        V(SalaryEmployees, MenuCodes.SalaryEmployees),
        V(SalaryPayroll, MenuCodes.SalaryPayroll),
        V(Reports, MenuCodes.Reports),
    ];

    private static Grant[] AccountantGrants() =>
    [
        V(Dashboard, MenuCodes.Dashboard),
        V(FeeManagement, MenuCodes.FeeManagement),
        Full(FeeMaster, MenuCodes.FeeMaster),
        Full(FeeCollection, MenuCodes.FeeCollection),
        V(SalaryManagement, MenuCodes.SalaryManagement),
        Full(SalaryStructure, MenuCodes.SalaryStructure),
        Full(SalaryEmployees, MenuCodes.SalaryEmployees),
        Full(SalaryPayroll, MenuCodes.SalaryPayroll),
        VAE(MyActions, MenuCodes.MyActions),
        V(Notices, MenuCodes.Notices),
        V(Reports, MenuCodes.Reports),
        V(Students, MenuCodes.Students),
        V(Academics, MenuCodes.Academics),
    ];

    private static Grant[] FrontOfficeExecutiveGrants() =>
    [
        V(Dashboard, MenuCodes.Dashboard),
        V(FrontOffice, MenuCodes.FrontOffice),
        Full(VisitorBook, MenuCodes.VisitorBook),
        Full(PhoneLogs, MenuCodes.PhoneLogs),
        Full(Complaints, MenuCodes.Complaints),
        Full(AdmissionInquiries, MenuCodes.AdmissionInquiries),
        Full(FrontOfficeSetup, MenuCodes.FrontOfficeSetup),
        VAE(MyActions, MenuCodes.MyActions),
        VAE(Notices, MenuCodes.Notices),
        V(Students, MenuCodes.Students),
        V(Academics, MenuCodes.Academics),
    ];
}
