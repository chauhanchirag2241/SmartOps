using SmartOps.Shared.Constants;

namespace SmartOps.Application.Configuration;

/// <summary>
/// Maps staff/persona labels (e.g. from teacher.role) to global <see cref="RoleNames"/> values.
/// Override via <c>PersonaRoleMapping:Mappings</c> in appsettings.
/// </summary>
public sealed class PersonaRoleMappingOptions
{
    public const string SectionName = "PersonaRoleMapping";

    public Dictionary<string, string> Mappings { get; set; } = CreateDefaultMappings();

    public static Dictionary<string, string> CreateDefaultMappings() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Teacher"] = RoleNames.Teacher,
            ["HOD"] = RoleNames.Hod,
            ["Head of Department"] = RoleNames.Hod,
            ["Student"] = RoleNames.Student,
            ["Parent"] = RoleNames.Parent,
            ["Accountant"] = RoleNames.Accountant,
            ["Staff"] = RoleNames.Staff,
            ["Clerk"] = RoleNames.Staff,
            ["School Admin"] = RoleNames.SchoolAdmin,
            ["Non-Teaching Staff"] = RoleNames.Staff,
        };
}
