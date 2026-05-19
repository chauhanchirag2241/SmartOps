namespace SmartOps.Application.Modules.Identity.Utilities;

/// <summary>
/// Builds default login passwords in the form <c>{Username}@{ddMMyyyy}</c>
/// (e.g. rahul + 10-05-2001 → Rahul@10052001).
/// </summary>
public static class DefaultPasswordGenerator
{
    public static string Generate(string username, DateOnly dateOfBirth)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        string displayName = CapitalizeUsername(username.Trim());
        string dobPart = dateOfBirth.ToString("ddMMyyyy", System.Globalization.CultureInfo.InvariantCulture);
        return $"{displayName}@{dobPart}";
    }

    private static string CapitalizeUsername(string username)
    {
        if (username.Length == 1)
        {
            return username.ToUpperInvariant();
        }

        return char.ToUpperInvariant(username[0]) + username[1..].ToLowerInvariant();
    }
}
