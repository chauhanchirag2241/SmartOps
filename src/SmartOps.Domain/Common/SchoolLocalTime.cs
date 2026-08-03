namespace SmartOps.Domain.Common;

/// <summary>
/// School wall-clock time (defaults to India / IST). Portal stores and compares business timestamps in IST, not UTC.
/// </summary>
public static class SchoolLocalTime
{
    private static readonly string[] DefaultTimeZoneIds = ["Asia/Kolkata", "India Standard Time"];

    /// <summary>Calendar date in the school timezone.</summary>
    public static DateOnly Today(string? timeZoneId = null) =>
        DateOnly.FromDateTime(Now(timeZoneId).DateTime);

    /// <summary>Current instant as DateTimeOffset in the school timezone (e.g. +05:30).</summary>
    public static DateTimeOffset Now(string? timeZoneId = null)
    {
        TimeZoneInfo zone = ResolveTimeZone(timeZoneId);
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
    }

    /// <summary>
    /// Current school local wall-clock as <see cref="DateTime"/> (Kind = Unspecified) for DB columns typed as timestamp without time zone.
    /// </summary>
    public static DateTime NowDateTime(string? timeZoneId = null)
    {
        DateTime local = Now(timeZoneId).DateTime;
        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId = null)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId) && TryFindTimeZone(timeZoneId, out TimeZoneInfo? configured))
        {
            return configured!;
        }

        foreach (string id in DefaultTimeZoneIds)
        {
            if (TryFindTimeZone(id, out TimeZoneInfo? fallback))
            {
                return fallback!;
            }
        }

        return TimeZoneInfo.Local;
    }

    private static bool TryFindTimeZone(string id, out TimeZoneInfo? zone)
    {
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            zone = null;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            zone = null;
            return false;
        }
    }
}
