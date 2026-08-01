namespace SmartOps.Domain.Common.Configuration;

/// <summary>
/// Database / schema / table name constants.
/// Schemas:
/// <list type="bullet">
/// <item><see cref="Schema_Global"/> — platform DB (<c>smartops_global</c>) catalog + ConfigUI identity</item>
/// <item><see cref="Schema_Man"/> — school DB management/identity (users, roles, permissions, branches, schoolsettings)</item>
/// <item><see cref="Schema_School"/> — school DB operational data</item>
/// </list>
/// </summary>
public static class DatabaseConfig
{
    /// <summary>Platform database schema (menus, widgets, schools, usertypes, ConfigUI users).</summary>
    public const string Schema_Global = "global";

    /// <summary>School database identity/management schema (users, roles, permissions, branches, settings).</summary>
    public const string Schema_Man = "man";

    /// <summary>School database operational schema (students, fees, attendance, …).</summary>
    public const string Schema_School = "school";

    public const string SystemUserId = "11111111-1111-1111-1111-111111111111";

    public const string TableUsers = "users";
    public const string TableRoles = "roles";
    public const string TableMenus = "menus";
    public const string TableUserRoles = "userroles";
    public const string TableRoleMenuPermissions = "rolemenupermissions";
    public const string TableDashboardWidgets = "dashboard_widgets";
    public const string TableRoleDashboardWidgetPermissions = "roledashboardwidgetpermissions";
    public const string TableSchools = "schools";
    public const string TableSchoolBranches = "schoolbranches";
    public const string TableUserBranchMappings = "userbranchmappings";
    public const string TableUserTypes = "usertypes";
    public const string TableSchoolSettings = "schoolsettings";
    public const string TableRefreshTokens = "refreshtokens";
    public const string TableEntityAuditLogs = "entity_audit_logs";

    // Students Module
    public const string TableStudents = "students";
    public const string TableStudentParents = "studentparents";
    public const string TableStudentAcademics = "studentacademics";
    public const string TableStudentPreviousSchools = "studentpreviousschools";
    public const string TableStudentCustomFields = "studentcustomfields";
    public const string TableStudentDocuments = "studentdocuments";

    // Class Module
    public const string TableAcademicYears = "academicyears";
    public const string TableClassAcademicPeriods = "classacademicperiods";
    public const string TableShifts = "shifts";
    public const string TableClassGroups = "classgroups";
    public const string TableClasses = "classes";

    // Subject Module
    public const string TableSubjects = "subjects";

    // Fee Module
    public const string TableFeeMaster = "feemaster";
    public const string TableFeeHead = "feehead";
    public const string TableFeeHeadPeriodAmount = "feeheadperiodamount";
    public const string TableFeeStudentAmount = "feestudentamount";
    public const string TableFeeMasterClassGroup = "feemasterclassgroup";
    public const string TableFeePayment = "feepayment";
    public const string TableFeePaymentLine = "feepaymentline";

    // Employee Module
    public const string TableEmployees = "employees";
    public const string TableDepartments = "departments";
    public const string TableClassSubjectTeacherMappings = "classsubjectteachermappings";
    public const string TableClassSettings = "classsettings";
    public const string TableHodDepartmentAssignments = "hoddepartmentassignments";
    public const string TableParentStudentMappings = "parentstudentmappings";
    public const string TableStaffScopeAssignments = "staffscopeassignments";

    public const string TableSettings = "settings";
    public const string TableAttendance = "attendance";
    public const string TableStaffAttendance = "staffattendance";
    public const string TableEmployeeFaceEnrollments = "employeefaceenrollments";
    public const string TableHomework = "homework";
    public const string TableHomeworkDetails = "homeworkdetails";

    // Salary Module
    public const string TableSalaryStructure = "salarystructure";
    public const string TableSalaryVersionComponents = "salaryversioncomponents";
    public const string TableEmployeeSalaries = "employeesalaries";
    public const string TableEmployeeSalaryComponents = "employeesalarycomponents";
    public const string TablePayrollRuns = "payrollruns";
    public const string TablePayrollEntries = "payrollentries";
    public const string TablePayrollEntryLines = "payrollentrylines";

    // Leave & Workflow Module
    public const string TableLeaveRequests = "leaverequests";
    public const string TableWorkflowItems = "workflowitems";
    public const string TableWorkflowItemActions = "workflowitemactions";
    public const string TableNotices = "notices";
    public const string TableNoticeResponses = "noticeresponses";

    // Exam Module
    public const string TableExamGradeScales = "examgradescales";
    public const string TableExamGradeScaleDetails = "examgradescaledetails";
    public const string TableExamGroups = "examgroups";
    public const string TableExams = "exams";
    public const string TableExamClasses = "examclasses";
    public const string TableExamMarkComponents = "exammarkcomponents";
    public const string TableExamSchedules = "examschedules";
    public const string TableExamStudentMarks = "examstudentmarks";
    public const string TableExamResults = "examresults";
    public const string TableExamHallTickets = "examhalltickets";

    // Front Office Module
    public const string TableComplaintTypes = "complainttypes";
    public const string TableVisitorPurposes = "visitorpurposes";
    public const string TableVisitors = "visitors";
    public const string TablePhoneLogs = "phonelogs";
    public const string TableComplaints = "complaints";
    public const string TableAdmissionInquiries = "admissioninquiries";

    // Timetable Module
    public const string TablePeriodTemplates = "period_templates";
    public const string TablePeriods = "periods";
    public const string TableClassTimetables = "class_timetables";
    public const string TableClassTimetableSlots = "class_timetable_slots";
}
