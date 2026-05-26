namespace SmartOps.Application.Modules.Authorization;

public static class DashboardClassLabel
{
    public const string SectionSuffixSql =
        "CASE c.section WHEN 1 THEN ' - A' WHEN 2 THEN ' - B' WHEN 3 THEN ' - C' WHEN 4 THEN ' - D' ELSE '' END";

    public static string Format(string? className, int section)
    {
        string baseName = (className ?? string.Empty).Trim();
        string suffix = SectionSuffix(section);
        return string.IsNullOrEmpty(baseName) ? suffix.TrimStart(' ', '-') : baseName + suffix;
    }

    public static string SectionSuffix(int section) => section switch
    {
        1 => " - A",
        2 => " - B",
        3 => " - C",
        4 => " - D",
        _ => string.Empty
    };
}
