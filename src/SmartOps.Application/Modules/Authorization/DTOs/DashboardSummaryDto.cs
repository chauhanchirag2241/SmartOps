namespace SmartOps.Application.Modules.Authorization.DTOs;

public sealed class DashboardSummaryDto
{
    public int TotalStudents { get; init; }

    public int TotalTeachers { get; init; }

    public int TotalClasses { get; init; }

    public int AttendanceMarkedToday { get; init; }

    public double AverageAttendancePercent { get; init; }

    public string ScopeLabel { get; init; } = string.Empty;
}
