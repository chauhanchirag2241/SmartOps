using SmartOps.Application.Modules.Authorization;
using Xunit;

namespace SmartOps.Application.Tests;

public sealed class DashboardAttendanceFilterTests
{
    [Fact]
    public void Resolve_Last7Days_SpansSevenDaysEndingToday()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        DashboardAttendanceDateRange range = DashboardAttendanceFilter.Resolve(
            DashboardAttendanceFilterPresets.Last7Days,
            null,
            null);

        Assert.Equal(today.AddDays(-6), range.From);
        Assert.Equal(today, range.To);
        Assert.Equal("Last 7 days", range.PeriodLabel);
    }

    [Fact]
    public void Resolve_Custom_UsesProvidedDates()
    {
        DateOnly from = new(2026, 5, 1);
        DateOnly to = new(2026, 5, 10);

        DashboardAttendanceDateRange range = DashboardAttendanceFilter.Resolve(
            DashboardAttendanceFilterPresets.Custom,
            from,
            to);

        Assert.Equal(from, range.From);
        Assert.Equal(to, range.To);
        Assert.Equal(DashboardAttendanceFilterPresets.Custom, range.Preset);
    }
}
