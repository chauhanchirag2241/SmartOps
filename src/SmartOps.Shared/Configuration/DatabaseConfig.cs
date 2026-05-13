namespace SmartOps.Shared.Configuration;

public static class DatabaseConfig
{
    public const string Schema_Global = "global";

    public const string SystemUserId = "11111111-1111-1111-1111-111111111111";

    public const string TableUsers = "users";
    public const string TableRoles = "roles";
    public const string TablePermissions = "permissions";
    public const string TableUserRoles = "userroles";
    public const string TableRolePermissions = "rolepermissions";
    public const string TableSchools = "schools";
    public const string TableUserSchoolMappings = "userschoolmappings";
    public const string TableRefreshTokens = "refreshtokens";
    
    // Students Module
    public const string TableStudents = "students";
    public const string TableStudentParents = "studentparents";
    public const string TableStudentAcademics = "studentacademics";
    public const string TableStudentPreviousSchools = "studentpreviousschools";
    public const string TableStudentFeeConfigs = "studentfeeconfigs";

    // Class Module
    public const string TableClasses = "classes";

    // Subject Module
    public const string TableSubjects = "subjects";
}
