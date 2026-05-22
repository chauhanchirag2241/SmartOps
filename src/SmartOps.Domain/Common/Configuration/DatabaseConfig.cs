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
    public const string TableSchools = "schools";
    public const string TableSchoolBranches = "schoolbranches";
    public const string TableUserSchoolMappings = "userschoolmappings";
    public const string TableRefreshTokens = "refreshtokens";
    public const string TableUserScopeVersions = "userscopeversions";
    public const string TableAuthorizationAuditLog = "authorizationauditlog";

    // Students Module
    public const string TableStudents = "students";
    public const string TableStudentParents = "studentparents";
    public const string TableStudentAcademics = "studentacademics";
    public const string TableStudentPreviousSchools = "studentpreviousschools";
    public const string TableStudentFeeConfigs = "studentfeeconfigs";
    public const string TableStudentCustomFields = "studentcustomfields";

    // Class Module
    public const string TableAcademicYears = "academicyears";
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
}
