namespace SmartOps.Application.Modules.Authorization;

public sealed class DashboardLayoutDto
{
    public string ScopeLabel { get; init; } = string.Empty;

    public string? AcademicYearLabel { get; init; }

    public string? SchoolName { get; init; }

    public IReadOnlyList<DashboardWidgetLayoutItemDto> Widgets { get; init; } = [];
}

public sealed class DashboardWidgetLayoutItemDto
{
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string DefaultSize { get; init; } = "stat";

    public int DisplayOrder { get; init; }

    public string RequiredMenuCode { get; init; } = string.Empty;
}

public sealed class DashboardQueryDto;

public sealed class DashboardResponseDto
{
    public string ScopeLabel { get; init; } = string.Empty;

    public IReadOnlyList<string> VisibleWidgets { get; init; } = [];

    public DashboardSummaryDto? Summary { get; init; }

    public AttendanceTodayDto? AttendanceToday { get; init; }

    public SalaryDashboardDto? Salary { get; init; }

    public decimal? SchoolFeesCollectedTotal { get; init; }

    public decimal? SchoolFeesDueTotal { get; init; }

    public IReadOnlyList<RecentStudentDto>? RecentStudents { get; init; }

    public IReadOnlyList<DashboardTeacherDto>? Teachers { get; init; }

    public IReadOnlyList<HomeworkDueDto>? HomeworkDue { get; init; }

    public IReadOnlyList<ClassOverviewDto>? ClassesOverview { get; init; }

    public DashboardAlertsDto? Alerts { get; init; }

    public int TotalSubjects { get; init; }
}

public sealed class AttendanceTodayDto
{
    public int Present { get; init; }

    public int Absent { get; init; }

    public int Leave { get; init; }

    public int Late { get; init; }

    public double PresentPercent { get; init; }

    public string DateLabel { get; init; } = string.Empty;

    public string PeriodLabel { get; init; } = string.Empty;
}

public sealed class SalaryDashboardDto
{
    public decimal DisbursedAmount { get; init; }

    public decimal PendingAmount { get; init; }

    public int PaidCount { get; init; }

    public int PendingCount { get; init; }

    public string PeriodLabel { get; init; } = string.Empty;

    public IReadOnlyList<SalaryCategoryDto> Categories { get; init; } = [];
}

public sealed class SalaryCategoryDto
{
    public string Label { get; init; } = string.Empty;

    public string SubLabel { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public bool IsPaid { get; init; }
}

public sealed class RecentStudentDto
{
    public Guid Id { get; init; }

    public string Initials { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string Badge { get; init; } = string.Empty;

    public string BadgeTone { get; init; } = "good";
}

public sealed class DashboardTeacherDto
{
    public string Initials { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string StatusTone { get; init; } = "good";
}

public sealed class HomeworkDueDto
{
    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string DueLabel { get; init; } = string.Empty;

    public string DueTone { get; init; } = "warn";
}

public sealed class ClassOverviewDto
{
    public string DisplayName { get; init; } = string.Empty;

    public int StudentCount { get; init; }

    public int Present { get; init; }

    public int Late { get; init; }

    public int Absent { get; init; }

    public int OnLeave { get; init; }

    public decimal FeeCollectedToday { get; init; }
}

public sealed class DashboardAlertsDto
{
    public IReadOnlyList<DashboardAlertItemDto> Items { get; init; } = [];
}

public sealed class DashboardAlertItemDto
{
    public string Icon { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string Tone { get; init; } = "danger";
}
