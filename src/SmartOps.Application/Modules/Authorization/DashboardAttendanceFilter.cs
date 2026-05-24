namespace SmartOps.Application.Modules.Authorization;

public static class DashboardAttendanceFilterPresets
{
    public const string Today = "today";
    public const string Yesterday = "yesterday";
    public const string Last7Days = "last7days";
    public const string ThisMonth = "thismonth";
    public const string LastMonth = "lastmonth";
    public const string Custom = "custom";
}

public sealed class DashboardAttendanceDateRange
{
    public string Preset { get; init; } = DashboardAttendanceFilterPresets.Today;

    public DateOnly From { get; init; }

    public DateOnly To { get; init; }

    public string PeriodLabel { get; init; } = string.Empty;
}

public static class DashboardAttendanceFilter
{
    public static DashboardAttendanceDateRange Resolve(
        string? preset,
        DateOnly? customFrom,
        DateOnly? customTo)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        string normalized = string.IsNullOrWhiteSpace(preset)
            ? DashboardAttendanceFilterPresets.Today
            : preset.Trim().ToLowerInvariant();

        return normalized switch
        {
            DashboardAttendanceFilterPresets.Yesterday => new DashboardAttendanceDateRange
            {
                Preset = normalized,
                From = today.AddDays(-1),
                To = today.AddDays(-1),
                PeriodLabel = "Yesterday"
            },
            DashboardAttendanceFilterPresets.Last7Days => new DashboardAttendanceDateRange
            {
                Preset = normalized,
                From = today.AddDays(-6),
                To = today,
                PeriodLabel = "Last 7 days"
            },
            DashboardAttendanceFilterPresets.ThisMonth => new DashboardAttendanceDateRange
            {
                Preset = normalized,
                From = new DateOnly(today.Year, today.Month, 1),
                To = today,
                PeriodLabel = "This month"
            },
            DashboardAttendanceFilterPresets.LastMonth => BuildLastMonth(today, normalized),
            DashboardAttendanceFilterPresets.Custom => BuildCustom(customFrom, customTo, today),
            _ => new DashboardAttendanceDateRange
            {
                Preset = DashboardAttendanceFilterPresets.Today,
                From = today,
                To = today,
                PeriodLabel = "Today"
            }
        };
    }

    private static DashboardAttendanceDateRange BuildLastMonth(DateOnly today, string preset)
    {
        DateOnly firstOfThisMonth = new(today.Year, today.Month, 1);
        DateOnly lastDay = firstOfThisMonth.AddDays(-1);
        DateOnly firstDay = new(lastDay.Year, lastDay.Month, 1);

        return new DashboardAttendanceDateRange
        {
            Preset = preset,
            From = firstDay,
            To = lastDay,
            PeriodLabel = "Last month"
        };
    }

    private static DashboardAttendanceDateRange BuildCustom(
        DateOnly? customFrom,
        DateOnly? customTo,
        DateOnly today)
    {
        if (customFrom is null || customTo is null || customFrom > customTo)
        {
            return new DashboardAttendanceDateRange
            {
                Preset = DashboardAttendanceFilterPresets.Today,
                From = today,
                To = today,
                PeriodLabel = "Today"
            };
        }

        string label = customFrom == customTo
            ? customFrom.Value.ToString("dd MMM yyyy")
            : $"{customFrom.Value:dd MMM yyyy} – {customTo.Value:dd MMM yyyy}";

        return new DashboardAttendanceDateRange
        {
            Preset = DashboardAttendanceFilterPresets.Custom,
            From = customFrom.Value,
            To = customTo.Value,
            PeriodLabel = label
        };
    }
}
