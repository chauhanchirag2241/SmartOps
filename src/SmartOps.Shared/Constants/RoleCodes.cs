namespace SmartOps.Shared.Constants;

public static class RoleCodes
{
    public const string Admin = "ADMIN";
    public const string SchoolAdmin = "SCHOOL_ADMIN";
    public const string Hod = "HOD";
    public const string Teacher = "TEACHER";
    public const string Student = "STUDENT";
    public const string Parent = "PARENT";
    public const string Accountant = "ACCOUNTANT";
    public const string Staff = "STAFF";

    public static readonly IReadOnlySet<string> GlobalScopeRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Admin,
        SchoolAdmin
    };
}
