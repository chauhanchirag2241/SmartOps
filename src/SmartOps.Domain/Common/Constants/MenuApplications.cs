namespace SmartOps.Domain.Common.Constants;

/// <summary>
/// Which Angular app a menu belongs to. COMMON menus appear in both apps.
/// </summary>
public static class MenuApplications
{
    public const string Common = "COMMON";

    public const string Config = "CONFIG";

    public const string School = "SCHOOL";

    public static bool IsValid(string? application) =>
        application is Common or Config or School;
}
