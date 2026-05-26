namespace SmartOps.Application.Abstractions;

/// <summary>Resolves calendar "today" in the school's timezone (defaults to India).</summary>
public static class SchoolLocalTime
{
    private static readonly string[] DefaultTimeZoneIds = ["Asia/Kolkata", "India Standard Time"];

    public static DateOnly Today(string? timeZoneId) =>
        DateOnly.FromDateTime(Now(timeZoneId).DateTime);

    public static DateTimeOffset Now(string? timeZoneId)
    {
        TimeZoneInfo zone = ResolveTimeZone(timeZoneId);
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId) && TryFindTimeZone(timeZoneId, out TimeZoneInfo? configured))
        {
            return configured;
        }

        foreach (string id in DefaultTimeZoneIds)
        {
            if (TryFindTimeZone(id, out TimeZoneInfo? fallback))
            {
                return fallback;
            }
        }

        return TimeZoneInfo.Local;
    }

    private static bool TryFindTimeZone(string id, out TimeZoneInfo zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            zone = TimeZoneInfo.Utc;
            return false;
        }
    }
}
