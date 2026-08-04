namespace SmartOps.Domain.Common.Constants;

/// <summary>Seeded school / platform role names.</summary>
public static class RoleNames
{
    /// <summary>Developer / platform management role (renamed from Admin).</summary>
    public const string SmartOpsAdmin = "SmartOpsAdmin";

    /// <summary>School operator admin (HR / full school portal).</summary>
    public const string SchoolAdmin = "School Admin";

    public const string Principal = "Principal";
    public const string Teacher = "Teacher";
    public const string Accountant = "Accountant";
    public const string FrontOfficeExecutive = "Front Office Executive";

    public static class Ids
    {
        public static readonly Guid SmartOpsAdmin = Guid.Parse("20000000-0000-0000-0000-000000000001");
        public static readonly Guid Principal = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public static readonly Guid Teacher = Guid.Parse("20000000-0000-0000-0000-000000000003");
        // 20000000-…0004 reserved — former Student portal role (mobile app role TBD)
        public static readonly Guid Accountant = Guid.Parse("20000000-0000-0000-0000-000000000005");
        public static readonly Guid FrontOfficeExecutive = Guid.Parse("20000000-0000-0000-0000-000000000006");
        public static readonly Guid SchoolAdmin = Guid.Parse("20000000-0000-0000-0000-000000000007");
    }

    public static readonly (Guid Id, string Name, string Description)[] Defaults =
    [
        (Ids.SmartOpsAdmin, SmartOpsAdmin, "SmartOps platform / developer management role"),
        (Ids.SchoolAdmin, SchoolAdmin, "School administrator — full school portal access"),
        (Ids.Principal, Principal, "School principal — academic and operational oversight"),
        (Ids.Teacher, Teacher, "Teaching staff — class, attendance, homework, exams"),
        (Ids.Accountant, Accountant, "Fees and salary operations"),
        (Ids.FrontOfficeExecutive, FrontOfficeExecutive, "Front office executive — visitors, inquiries, complaints"),
    ];

    /// <summary>Roles that receive full SCHOOL + COMMON menu grants on school DB seed.</summary>
    public static readonly string[] FullAccessSchoolRoles =
    [
        SmartOpsAdmin,
        SchoolAdmin,
    ];

    /// <summary>Maps a seeded user type name to the matching default portal role (or null = no auto role).</summary>
    public static string? FromUserType(string? userTypeName)
    {
        if (string.IsNullOrWhiteSpace(userTypeName))
        {
            return null;
        }

        string t = userTypeName.Trim();

        // Student user type: no SmartOpsUI portal role (mobile app role later).
        if (string.Equals(t, UserTypeCodes.Student, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(t, UserTypeCodes.Teacher, StringComparison.OrdinalIgnoreCase))
        {
            return Teacher;
        }

        if (string.Equals(t, UserTypeCodes.Principal, StringComparison.OrdinalIgnoreCase))
        {
            return Principal;
        }

        if (string.Equals(t, UserTypeCodes.Accountant, StringComparison.OrdinalIgnoreCase))
        {
            return Accountant;
        }

        if (string.Equals(t, UserTypeCodes.FrontOfficeExecutive, StringComparison.OrdinalIgnoreCase))
        {
            return FrontOfficeExecutive;
        }

        if (string.Equals(t, UserTypeCodes.SchoolAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return SchoolAdmin;
        }

        if (string.Equals(t, UserTypeCodes.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return SmartOpsAdmin;
        }

        // Office staff / Non-academic staff — no automatic role
        return null;
    }

    public static bool IsDefaultRole(string? roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            return false;
        }

        foreach ((Guid _, string name, string _) in Defaults)
        {
            if (string.Equals(name, roleName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsFullAccessRole(string? roleName) =>
        !string.IsNullOrWhiteSpace(roleName)
        && FullAccessSchoolRoles.Any(r => string.Equals(r, roleName.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Platform / developer role — never listed or assignable in the school portal UI.
    /// </summary>
    public static bool IsHiddenFromPortal(string? roleName) =>
        !string.IsNullOrWhiteSpace(roleName)
        && string.Equals(roleName.Trim(), SmartOpsAdmin, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc cref="IsHiddenFromPortal(string?)"/>
    public static bool IsHiddenFromPortal(Guid roleId) =>
        roleId == Ids.SmartOpsAdmin;

    /// <summary>
    /// Prefer an explicit portal role label when it matches a seeded role;
    /// otherwise map from user type (may be null).
    /// </summary>
    public static string? ResolveForProvision(string? userTypeName, string? preferredRoleName = null)
    {
        if (!string.IsNullOrWhiteSpace(preferredRoleName)
            && IsDefaultRole(preferredRoleName)
            && !IsHiddenFromPortal(preferredRoleName))
        {
            foreach ((Guid _, string name, string _) in Defaults)
            {
                if (string.Equals(name, preferredRoleName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }
        }

        string? fromType = FromUserType(userTypeName);
        return IsHiddenFromPortal(fromType) ? null : fromType;
    }
}
