namespace SmartOps.Domain.Common.Constants;

/// <summary>Canonical user type <c>name</c> values seeded in <c>global.usertypes</c> (no code column).</summary>
public static class UserTypeCodes
{
    public const string Admin = "Admin";
    public const string SchoolAdmin = "School Admin";
    public const string Principal = "Principal";
    public const string Student = "Student";
    public const string Teacher = "Teacher";
    public const string Accountant = "Accountant";
    public const string NonAcademicStaff = "Non-academic staff";
    public const string OfficeStaff = "Office staff";
    public const string FrontOfficeExecutive = "Front Office Executive";

    public static class Ids
    {
        public static readonly Guid Admin = Guid.Parse("30000000-0000-0000-0000-000000000001");
        public static readonly Guid SchoolAdmin = Guid.Parse("30000000-0000-0000-0000-000000000002");
        public static readonly Guid Principal = Guid.Parse("30000000-0000-0000-0000-000000000003");
        public static readonly Guid Student = Guid.Parse("30000000-0000-0000-0000-000000000004");
        public static readonly Guid Teacher = Guid.Parse("30000000-0000-0000-0000-000000000005");
        public static readonly Guid Accountant = Guid.Parse("30000000-0000-0000-0000-000000000006");
        public static readonly Guid NonAcademicStaff = Guid.Parse("30000000-0000-0000-0000-000000000007");
        public static readonly Guid OfficeStaff = Guid.Parse("30000000-0000-0000-0000-000000000008");
        public static readonly Guid FrontOfficeExecutive = Guid.Parse("30000000-0000-0000-0000-000000000009");
    }

    /// <summary>Seed order for <c>global.usertypes</c>.</summary>
    public static readonly (Guid Id, string Name)[] All =
    [
        (Ids.Admin, Admin),
        (Ids.SchoolAdmin, SchoolAdmin),
        (Ids.Principal, Principal),
        (Ids.Student, Student),
        (Ids.Teacher, Teacher),
        (Ids.Accountant, Accountant),
        (Ids.NonAcademicStaff, NonAcademicStaff),
        (Ids.OfficeStaff, OfficeStaff),
        (Ids.FrontOfficeExecutive, FrontOfficeExecutive),
    ];

    public static readonly IReadOnlySet<string> GlobalScopeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Admin,
        SchoolAdmin,
        Principal,
        OfficeStaff,
        FrontOfficeExecutive,
    };

    public static bool IsStaff(string? name) =>
        string.Equals(name, Teacher, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, Accountant, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, NonAcademicStaff, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, OfficeStaff, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, FrontOfficeExecutive, StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, Principal, StringComparison.OrdinalIgnoreCase);

    public static bool IsGlobalScope(string? name) =>
        !string.IsNullOrWhiteSpace(name) && GlobalScopeTypes.Contains(name);

    public static Guid? TryGetId(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        foreach ((Guid id, string n) in All)
        {
            if (string.Equals(n, name.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>Resolves display name for a usertype id from <see cref="All"/> (no DB lookup).</summary>
    public static string? GetName(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        foreach ((Guid knownId, string name) in All)
        {
            if (knownId == id)
            {
                return name;
            }
        }

        return null;
    }
}
