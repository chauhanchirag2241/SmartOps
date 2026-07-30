namespace SmartOps.Application.Modules.Authorization;

public static class DashboardClassLabel
{
    public const string SectionSuffixSql = "' - ' || c.section";

    /// <summary>Requires aliases <c>cg</c> (classgroups) and <c>c</c> (classes).</summary>
    public const string DisplayNameSql = "cg.classname || " + SectionSuffixSql;

    public static string Format(string? className, string? section)
    {
        string baseName = (className ?? string.Empty).Trim();
        string sec = (section ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(sec))
        {
            return baseName;
        }

        return string.IsNullOrEmpty(baseName) ? sec : $"{baseName} - {sec}";
    }
}
