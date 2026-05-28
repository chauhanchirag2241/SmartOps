namespace SmartOps.Domain.Common.Configuration;

public static class DatabaseConfig
{
    public const string Schema_Global = "global";

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
    public const string TableUserSchoolMappings = "userschoolmappings";
    public const string TableRefreshTokens = "refreshtokens";
    public const string TableUserScopeVersions = "userscopeversions";
    public const string TableAuthorizationAuditLog = "authorizationauditlog";
    public const string TableEntityAuditLogs = "entity_audit_logs";

    // Students Module
    public const string TableStudents = "students";
    public const string TableStudentParents = "studentparents";
    public const string TableStudentAcademics = "studentacademics";
    public const string TableStudentPreviousSchools = "studentpreviousschools";
    public const string TableStudentFeeHeadAssignments = "studentfeeheadassignments";
    public const string TableStudentFeeInstallments = "studentfeeinstallments";
    public const string TableStudentCustomFields = "studentcustomfields";

    // Class Module
    public const string TableAcademicYears = "academicyears";
    public const string TableAcademicYearSemesters = "academicyearsemesters";
    public const string TableClasses = "classes";

    // Subject Module
    public const string TableSubjects = "subjects";

    // Teacher Module
    public const string TableTeachers = "teachers";
    public const string TableDepartments = "departments";
    public const string TableClassSubjectTeacherMappings = "classsubjectteachermappings";
    public const string TableHodDepartmentAssignments = "hoddepartmentassignments";
    public const string TableParentStudentMappings = "parentstudentmappings";
    public const string TableStaffScopeAssignments = "staffscopeassignments";

    // School template schema (tenant tables cloned from here)
    public const string Schema_School = "school";

    public const string TableSettings = "settings";
    public const string TableAlerts = "alerts";
    public const string TableAttendance = "attendance";
    public const string TableHomework = "homework";
    public const string TableHomeworkDetails = "homeworkdetails";

    // Fees Module
    public const string TableFeeStructureVersions = "feestructureversions";
    public const string TableFeeTypes = "feetypes";
    public const string TableFeeSettings = "feesettings";
    public const string TableClassFeeAmounts = "classfeeamounts";
    public const string TableClassFeeInstallments = "classfeeinstallments";
    public const string TableFeePayments = "feepayments";
    public const string TableFeePaymentAllocations = "feepaymentallocations";

    // Salary Module
    public const string TableSalaryStructureVersions = "salarystructureversions";
    public const string TableSalaryVersionComponents = "salaryversioncomponents";
    public const string TableEmployeeSalaries = "employeesalaries";
    public const string TableEmployeeSalaryComponents = "employeesalarycomponents";
    public const string TablePayrollRuns = "payrollruns";
    public const string TablePayrollEntries = "payrollentries";
    public const string TablePayrollEntryLines = "payrollentrylines";
}
