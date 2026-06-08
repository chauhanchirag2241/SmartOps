using Dapper;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Authorization;
using SmartOps.Application.Modules.Authorization.Interfaces;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Common.Enums;
using SmartOps.Infrastructure.Modules.Authorization.Sql;
using SmartOps.Infrastructure.Persistence.Context;
using System.Data;
using System.Globalization;

namespace SmartOps.Infrastructure.Modules.Authorization.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly DapperContext _context;
    private readonly IUserScopeContext _scope;
    private readonly ICurrentUserService _currentUser;
    private readonly IDashboardWidgetPermissionService _widgetPermissions;
    private readonly ITenantProvider _tenantProvider;

    public DashboardService(
        DapperContext context,
        IUserScopeContext scope,
        ICurrentUserService currentUser,
        IDashboardWidgetPermissionService widgetPermissions,
        ITenantProvider tenantProvider)
    {
        _context = context;
        _scope = scope;
        _currentUser = currentUser;
        _widgetPermissions = widgetPermissions;
        _tenantProvider = tenantProvider;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        DashboardResponseDto dashboard = await GetDashboardAsync(cancellationToken).ConfigureAwait(false);
        DashboardSummaryDto? summary = dashboard.Summary;
        if (summary is not null)
        {
            return summary;
        }

        return new DashboardSummaryDto
        {
            ScopeLabel = dashboard.ScopeLabel,
            AverageAttendancePercent = dashboard.AttendanceToday?.PresentPercent ?? 0
        };
    }

    public async Task<DashboardLayoutDto> GetLayoutAsync(CancellationToken cancellationToken = default)
    {
        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<DashboardWidgetLayoutItemDto> widgets = await _widgetPermissions
            .GetVisibleWidgetsAsync(cancellationToken)
            .ConfigureAwait(false);

        (string? academicYearLabel, string? schoolName) = await LoadContextLabelsAsync(cancellationToken).ConfigureAwait(false);

        return new DashboardLayoutDto
        {
            ScopeLabel = BuildScopeLabel(),
            AcademicYearLabel = academicYearLabel,
            SchoolName = schoolName,
            Widgets = widgets
        };
    }

    public async Task<DashboardResponseDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        await _scope.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<DashboardWidgetLayoutItemDto> widgets = await _widgetPermissions
            .GetVisibleWidgetsAsync(cancellationToken)
            .ConfigureAwait(false);

        HashSet<string> visible = widgets.Select(w => w.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string schema = _context.OperationalSchema;
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        bool hasAttendanceWidgets = HasAny(visible, DashboardWidgetCodes.AttendanceDetail, DashboardWidgetCodes.AttendanceRate);
        DateOnly schoolToday = await ResolveSchoolTodayAsync(cancellationToken).ConfigureAwait(false);
        DashboardAttendanceDateRange attendanceRange = BuildTodayRange(schoolToday);

        DashboardSummaryDto? summary = null;
        if (HasAny(visible,
                DashboardWidgetCodes.StudentsStat,
                DashboardWidgetCodes.TeachersStat,
                DashboardWidgetCodes.ClassesStat,
                DashboardWidgetCodes.AttendanceRate))
        {
            summary = await LoadSummaryAsync(
                connection,
                schema,
                schoolToday,
                hasAttendanceWidgets ? attendanceRange : null,
                cancellationToken).ConfigureAwait(false);
        }

        AttendanceTodayDto? attendanceToday = null;
        if (hasAttendanceWidgets)
        {
            attendanceToday = await LoadAttendanceAsync(connection, schema, attendanceRange, cancellationToken)
                .ConfigureAwait(false);
            if (summary is not null && attendanceToday is not null)
            {
                summary = new DashboardSummaryDto
                {
                    TotalStudents = summary.TotalStudents,
                    TotalTeachers = summary.TotalTeachers,
                    TotalClasses = summary.TotalClasses,
                    AttendanceMarkedToday = summary.AttendanceMarkedToday,
                    AverageAttendancePercent = attendanceToday.PresentPercent,
                    ScopeLabel = summary.ScopeLabel
                };
            }
        }

        SalaryDashboardDto? salary = null;
        if (HasAny(visible, DashboardWidgetCodes.SalaryDisbursed, DashboardWidgetCodes.SalaryStatus))
        {
            salary = await LoadSalaryAsync(connection, schema, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<RecentStudentDto>? recentStudents = null;
        if (visible.Contains(DashboardWidgetCodes.RecentStudents))
        {
            recentStudents = await LoadRecentStudentsAsync(connection, schema, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<DashboardTeacherDto>? teachers = null;
        if (visible.Contains(DashboardWidgetCodes.TeachersList))
        {
            teachers = await LoadTeachersAsync(connection, schema, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<HomeworkDueDto>? homework = null;
        if (visible.Contains(DashboardWidgetCodes.HomeworkDue))
        {
            homework = await LoadHomeworkDueAsync(connection, schema, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<ClassOverviewDto>? classesOverview = null;
        if (visible.Contains(DashboardWidgetCodes.ClassesOverview))
        {
            classesOverview = await LoadClassesOverviewAsync(connection, schema, schoolToday, cancellationToken)
                .ConfigureAwait(false);
        }

        int totalSubjects = 0;
        if (visible.Contains(DashboardWidgetCodes.SubjectsStat))
        {
            totalSubjects = await CountSubjectsAsync(connection, schema, cancellationToken).ConfigureAwait(false);
            if (summary is not null)
            {
                // Subjects count exposed via TotalSubjects on response
            }
        }

        DashboardAlertsDto? alerts = null;
        if (visible.Contains(DashboardWidgetCodes.AlertsActions))
        {
            alerts = BuildAlerts(attendanceToday, homework, salary);
        }

        return new DashboardResponseDto
        {
            ScopeLabel = BuildScopeLabel(),
            VisibleWidgets = widgets.Select(w => w.Code).ToList(),
            Summary = summary,
            AttendanceToday = attendanceToday,
            Salary = salary,
            RecentStudents = recentStudents,
            Teachers = teachers,
            HomeworkDue = homework,
            ClassesOverview = classesOverview,
            Alerts = alerts,
            TotalSubjects = totalSubjects
        };
    }

    private async Task<DateOnly> ResolveSchoolTodayAsync(CancellationToken cancellationToken)
    {
        string? timeZoneId = await LoadSchoolTimeZoneAsync(cancellationToken).ConfigureAwait(false);
        return SchoolLocalTime.Today(timeZoneId);
    }

    private async Task<string?> LoadSchoolTimeZoneAsync(CancellationToken cancellationToken)
    {
        string? schoolId = _tenantProvider.GetCurrentSchoolId();
        if (string.IsNullOrWhiteSpace(schoolId) || !Guid.TryParse(schoolId, out Guid sid))
        {
            return null;
        }

        IDbConnection platform = await _context.GetPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await platform.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                $"""
SELECT timezone FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools}
WHERE id = @Id AND isactive = true LIMIT 1
""",
                new { Id = sid },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static DashboardAttendanceDateRange BuildTodayRange(DateOnly schoolToday) =>
        new()
        {
            Preset = DashboardAttendanceFilterPresets.Today,
            From = schoolToday,
            To = schoolToday,
            PeriodLabel = "Today"
        };

    private async Task<DashboardSummaryDto> LoadSummaryAsync(
        IDbConnection connection,
        string schema,
        DateOnly schoolToday,
        DashboardAttendanceDateRange? attendanceRange,
        CancellationToken cancellationToken)
    {
        string studentFilter = BuildStudentExistsFilter(schema, "s");
        string classFilter = BuildClassFilter(schema, "c");
        string teacherFilter = BuildTeacherFilter(schema, "t");

        string attendanceDateFilter = attendanceRange is null
            ? "a.attendancedate = @SchoolToday"
            : "a.attendancedate >= @AttendanceFromDate AND a.attendancedate <= @AttendanceToDate";

        string sql = $"""
SELECT
    (SELECT COUNT(*) FROM {schema}.{DatabaseConfig.TableStudents} s WHERE s.isactive = true {studentFilter}) AS TotalStudents,
    (SELECT COUNT(*) FROM {schema}.{DatabaseConfig.TableTeachers} t WHERE t.isactive = true {teacherFilter}) AS TotalTeachers,
    (SELECT COUNT(*) FROM {schema}.{DatabaseConfig.TableClasses} c WHERE c.isactive = true {classFilter}) AS TotalClasses,
    (SELECT COUNT(*) FROM {schema}.{DatabaseConfig.TableAttendance} a
        WHERE {attendanceDateFilter} AND a.isactive = true
        {BuildAttendanceFilter(schema, "a")}) AS AttendanceMarkedToday
""";

        DashboardRow row = await connection.QuerySingleAsync<DashboardRow>(
            new CommandDefinition(
                sql,
                BuildParameters(attendanceRange, schoolToday),
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return new DashboardSummaryDto
        {
            TotalStudents = row.TotalStudents,
            TotalTeachers = row.TotalTeachers,
            TotalClasses = row.TotalClasses,
            AttendanceMarkedToday = row.AttendanceMarkedToday,
            AverageAttendancePercent = 0,
            ScopeLabel = BuildScopeLabel()
        };
    }

    private async Task<AttendanceTodayDto?> LoadAttendanceAsync(
        IDbConnection connection,
        string schema,
        DashboardAttendanceDateRange attendanceRange,
        CancellationToken cancellationToken)
    {
        string filter = BuildAttendanceFilter(schema, "a");
        string sql = $"""
SELECT
    COUNT(*) FILTER (WHERE a.status = 1) AS Present,
    COUNT(*) FILTER (WHERE a.status = 2) AS Absent,
    COUNT(*) FILTER (WHERE a.status = 3) AS Leave,
    COUNT(*) FILTER (WHERE a.status = 4) AS Late
FROM {schema}.{DatabaseConfig.TableAttendance} a
WHERE a.attendancedate >= @AttendanceFromDate
  AND a.attendancedate <= @AttendanceToDate
  AND a.isactive = true {filter}
""";

        AttendanceRow row = await connection.QuerySingleAsync<AttendanceRow>(
            new CommandDefinition(
                sql,
                BuildParameters(attendanceRange, attendanceRange.From),
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        int total = row.Present + row.Absent + row.Leave + row.Late;
        int attended = row.Present + row.Late;
        double percent = total > 0 ? Math.Round(attended * 100.0 / total, 1) : 0;

        return new AttendanceTodayDto
        {
            Present = row.Present,
            Absent = row.Absent,
            Leave = row.Leave,
            Late = row.Late,
            PresentPercent = percent,
            DateLabel = FormatAttendanceDateLabel(attendanceRange.From, attendanceRange.To),
            PeriodLabel = attendanceRange.PeriodLabel
        };
    }

    private static string FormatAttendanceDateLabel(DateOnly from, DateOnly to) =>
        from == to
            ? from.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
            : $"{from:dd MMM yyyy} – {to:dd MMM yyyy}";

    private async Task<SalaryDashboardDto?> LoadSalaryAsync(
        IDbConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        string sql = $"""
SELECT
    pr.totalnet AS TotalNet,
    pr.status AS RunStatus,
    pr.employeecount AS EmployeeCount
FROM {schema}.{DatabaseConfig.TablePayrollRuns} pr
WHERE pr.payyear = @Year AND pr.paymonth = @Month AND pr.isactive = true
ORDER BY pr.processedon DESC NULLS LAST
LIMIT 1
""";

        PayrollRunRow? run = await connection.QuerySingleOrDefaultAsync<PayrollRunRow>(
            new CommandDefinition(sql, new { Year = now.Year, Month = now.Month }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        string entriesSql = $"""
SELECT
    COUNT(*) FILTER (WHERE pe.status >= 1) AS PaidCount,
    COUNT(*) FILTER (WHERE pe.status < 1) AS PendingCount,
    COALESCE(SUM(pe.netsalary) FILTER (WHERE pe.status >= 1), 0) AS PaidAmount,
    COALESCE(SUM(pe.netsalary) FILTER (WHERE pe.status < 1), 0) AS PendingAmount
FROM {schema}.{DatabaseConfig.TablePayrollEntries} pe
INNER JOIN {schema}.{DatabaseConfig.TablePayrollRuns} pr ON pr.id = pe.payrollrunid AND pr.isactive = true
WHERE pr.payyear = @Year AND pr.paymonth = @Month AND pe.isactive = true
""";

        PayrollStatsRow stats = await connection.QuerySingleOrDefaultAsync<PayrollStatsRow>(
            new CommandDefinition(entriesSql, new { Year = now.Year, Month = now.Month }, cancellationToken: cancellationToken))
            .ConfigureAwait(false)
            ?? new PayrollStatsRow();

        decimal disbursed = stats.PaidAmount > 0 ? stats.PaidAmount : run?.TotalNet ?? 0;
        string period = now.ToString("MMM yyyy", CultureInfo.InvariantCulture);

        return new SalaryDashboardDto
        {
            DisbursedAmount = disbursed,
            PendingAmount = stats.PendingAmount,
            PaidCount = stats.PaidCount,
            PendingCount = stats.PendingCount,
            PeriodLabel = period,
            Categories =
            [
                new SalaryCategoryDto
                {
                    Label = "Payroll",
                    SubLabel = $"{stats.PaidCount + stats.PendingCount} employees",
                    Amount = disbursed,
                    IsPaid = stats.PendingCount == 0
                }
            ]
        };
    }

    private async Task<IReadOnlyList<RecentStudentDto>> LoadRecentStudentsAsync(
        IDbConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        string filter = BuildStudentExistsFilter(schema, "s");
        string sql = $"""
SELECT
    s.id AS Id,
    TRIM(CONCAT(s.firstname, ' ', s.lastname)) AS Name,
    c.classname AS ClassName,
    c.section AS Section,
    s.createdon AS CreatedOn
FROM {schema}.{DatabaseConfig.TableStudents} s
LEFT JOIN {schema}.{DatabaseConfig.TableStudentAcademics} sa ON sa.studentid = s.id
    AND {AcademicYearScopeSql.StudentAcademicEnrollmentVisibilityClause()}
LEFT JOIN {schema}.{DatabaseConfig.TableClasses} c ON c.id = sa.classid AND c.isactive = true
WHERE s.isactive = true {filter}
ORDER BY s.createdon DESC
LIMIT 5
""";

        IEnumerable<RecentStudentRow> rows = await connection.QueryAsync<RecentStudentRow>(
            new CommandDefinition(sql, BuildParameters(), cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new RecentStudentDto
        {
            Id = r.Id,
            Initials = BuildInitials(r.Name),
            Name = r.Name,
            Detail = DashboardClassLabel.Format(r.ClassName, r.Section),
            Badge = "New",
            BadgeTone = "good"
        }).ToList();
    }

    private async Task<IReadOnlyList<DashboardTeacherDto>> LoadTeachersAsync(
        IDbConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        string filter = BuildTeacherFilter(schema, "t");
        string sql = $"""
SELECT
    TRIM(CONCAT(t.firstname, ' ', t.lastname)) AS Name
FROM {schema}.{DatabaseConfig.TableTeachers} t
WHERE t.isactive = true {filter}
ORDER BY t.firstname, t.lastname
LIMIT 5
""";

        IEnumerable<string> names = await connection.QueryAsync<string>(
            new CommandDefinition(sql, BuildParameters(), cancellationToken: cancellationToken)).ConfigureAwait(false);

        return names.Select(n => new DashboardTeacherDto
        {
            Initials = BuildInitials(n),
            Name = n,
            Detail = "Staff",
            Status = "Active",
            StatusTone = "good"
        }).ToList();
    }

    private async Task<IReadOnlyList<HomeworkDueDto>> LoadHomeworkDueAsync(
        IDbConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        string classFilter = !_scope.ScopesEnabled || _scope.IsGlobalScope
            ? string.Empty
            : _scope.AllowedClassIds.Count == 0
                ? " AND 1 = 0"
                : " AND h.classid = ANY(@ScopeClassIds)";
        string sql = $"""
SELECT
    h.title AS Title,
    c.classname AS ClassName,
    c.section AS Section,
    h.duedate AS DueDate
FROM {schema}.{DatabaseConfig.TableHomework} h
INNER JOIN {schema}.{DatabaseConfig.TableClasses} c ON c.id = h.classid AND c.isactive = true
WHERE h.isactive = true
  AND h.duedate >= CURRENT_DATE
  AND h.duedate <= CURRENT_DATE + 3
  AND (@ScopeAcademicYearId IS NULL OR c.academicyearid = @ScopeAcademicYearId)
  {classFilter}
ORDER BY h.duedate ASC
LIMIT 5
""";

        IEnumerable<HomeworkRow> rows = await connection.QueryAsync<HomeworkRow>(
            new CommandDefinition(sql, BuildParameters(), cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r =>
        {
            int days = (r.DueDate.Date - DateTime.UtcNow.Date).Days;
            string label = days <= 0 ? "Today" : days == 1 ? "Tomorrow" : r.DueDate.ToString("dd MMM", CultureInfo.InvariantCulture);
            string tone = days <= 0 ? "alert" : "warn";
            return new HomeworkDueDto
            {
                Title = r.Title,
                Subtitle = DashboardClassLabel.Format(r.ClassName, r.Section),
                DueLabel = label,
                DueTone = tone
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<ClassOverviewDto>> LoadClassesOverviewAsync(
        IDbConnection connection,
        string schema,
        DateOnly schoolToday,
        CancellationToken cancellationToken)
    {
        string classFilter = BuildClassFilter(schema, "c");
        string studentScope = BuildStudentExistsFilter(schema, "st");
        string feeStudentScope = studentScope.Replace("st.", "st2.", StringComparison.Ordinal);
        string sql = $"""
SELECT
    c.classname AS ClassName,
    c.section AS Section,
    COUNT(DISTINCT sa.studentid) AS StudentCount,
    COUNT(DISTINCT a.studentid) FILTER (WHERE a.status = 1) AS Present,
    COUNT(DISTINCT a.studentid) FILTER (WHERE a.status = 4) AS Late,
    COUNT(DISTINCT a.studentid) FILTER (WHERE a.status = 2) AS Absent,
    COUNT(DISTINCT a.studentid) FILTER (WHERE a.status = 3) AS OnLeave,
    COALESCE((
        SELECT SUM(fp.amount)
        FROM {schema}.{DatabaseConfig.TableFeePayments} fp
        INNER JOIN {schema}.{DatabaseConfig.TableStudents} st2 ON st2.id = fp.studentid AND st2.isactive = true
        INNER JOIN {schema}.{DatabaseConfig.TableStudentAcademics} sa2 ON sa2.studentid = st2.id AND sa2.classid = c.id
            AND {AcademicYearScopeSql.StudentAcademicEnrollmentVisibilityClause("sa2")}
        WHERE fp.isactive = true AND fp.paymentdate = @SchoolToday {feeStudentScope}
    ), 0) AS FeeCollectedToday
FROM {schema}.{DatabaseConfig.TableClasses} c
LEFT JOIN {schema}.{DatabaseConfig.TableStudentAcademics} sa ON sa.classid = c.id
    AND {AcademicYearScopeSql.StudentAcademicEnrollmentVisibilityClause()}
LEFT JOIN {schema}.{DatabaseConfig.TableStudents} st ON st.id = sa.studentid AND st.isactive = true {studentScope}
LEFT JOIN {schema}.{DatabaseConfig.TableAttendance} a ON a.classid = c.id AND a.studentid = sa.studentid
    AND a.attendancedate = @SchoolToday AND a.isactive = true
WHERE c.isactive = true {classFilter}
GROUP BY c.id, c.classname, c.section
ORDER BY c.classname, c.section
""";

        IEnumerable<ClassOverviewRow> rows = await connection.QueryAsync<ClassOverviewRow>(
            new CommandDefinition(sql, BuildParameters(schoolToday: schoolToday), cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        return rows.Select(r => new ClassOverviewDto
        {
            DisplayName = DashboardClassLabel.Format(r.ClassName, r.Section),
            StudentCount = r.StudentCount,
            Present = r.Present,
            Late = r.Late,
            Absent = r.Absent,
            OnLeave = r.OnLeave,
            FeeCollectedToday = r.FeeCollectedToday
        }).ToList();
    }

    private async Task<int> CountSubjectsAsync(
        IDbConnection connection,
        string schema,
        CancellationToken cancellationToken)
    {
        string sql = $"""
SELECT COUNT(*) FROM {schema}.{DatabaseConfig.TableSubjects} sub
WHERE sub.isactive = true
""";
        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private async Task<(string? AcademicYear, string? SchoolName)> LoadContextLabelsAsync(CancellationToken cancellationToken)
    {
        string schema = _context.OperationalSchema;
        IDbConnection connection = await _context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);

        string? yearLabel = null;
        if (_scope.ActiveAcademicYearId.HasValue)
        {
            yearLabel = await connection.QuerySingleOrDefaultAsync<string>(
                new CommandDefinition(
                    $"""
SELECT title FROM {schema}.{DatabaseConfig.TableAcademicYears}
WHERE id = @Id AND isactive = true LIMIT 1
""",
                    new { Id = _scope.ActiveAcademicYearId },
                    cancellationToken: cancellationToken)).ConfigureAwait(false);
        }

        string? schoolName = await LoadSchoolNameAsync(cancellationToken).ConfigureAwait(false);

        return (yearLabel, schoolName);
    }

    private async Task<string?> LoadSchoolNameAsync(CancellationToken cancellationToken)
    {
        string? schoolId = _tenantProvider.GetCurrentSchoolId();
        if (string.IsNullOrWhiteSpace(schoolId) || !Guid.TryParse(schoolId, out Guid sid))
        {
            return null;
        }

        IDbConnection platform = await _context.GetPlatformConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await platform.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                $"""
SELECT name FROM {DatabaseConfig.Schema_Global}.{DatabaseConfig.TableSchools}
WHERE id = @Id AND isactive = true LIMIT 1
""",
                new { Id = sid },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static DashboardAlertsDto BuildAlerts(
        AttendanceTodayDto? attendance,
        IReadOnlyList<HomeworkDueDto>? homework,
        SalaryDashboardDto? salary)
    {
        List<DashboardAlertItemDto> items = new();

        if (attendance is { Absent: > 0 })
        {
            string period = string.IsNullOrWhiteSpace(attendance.PeriodLabel) ? "selected period" : attendance.PeriodLabel.ToLowerInvariant();
            items.Add(new DashboardAlertItemDto
            {
                Icon = "user-x",
                Title = $"{attendance.Absent} absent",
                Subtitle = $"Review attendance · {period}",
                Tone = "warning"
            });
        }

        if (homework is { Count: > 0 })
        {
            items.Add(new DashboardAlertItemDto
            {
                Icon = "pencil",
                Title = $"{homework.Count} homework due soon",
                Subtitle = "Next 3 days",
                Tone = "success"
            });
        }

        if (salary is { PendingCount: > 0 })
        {
            items.Add(new DashboardAlertItemDto
            {
                Icon = "report-money",
                Title = $"{salary.PendingCount} salary pending",
                Subtitle = salary.PeriodLabel,
                Tone = "danger"
            });
        }

        return new DashboardAlertsDto { Items = items };
    }

    private string BuildScopeLabel() => _scope.ScopeType switch
    {
        DataScopeType.Global => "All school data",
        DataScopeType.Class => "Your assigned classes",
        DataScopeType.Department => "Your department",
        DataScopeType.LinkedStudents => "Your children",
        DataScopeType.Self => "Your profile",
        DataScopeType.ModuleOnly => "Accounts module",
        _ => "Limited access"
    };

    private bool UseSchoolWideFinance() =>
        _scope.ScopeType == DataScopeType.ModuleOnly || _scope.IsGlobalScope;

    private static bool HasAny(HashSet<string> visible, params string[] codes) =>
        codes.Any(visible.Contains);

    private static string BuildInitials(string name)
    {
        string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        if (parts.Length == 1)
        {
            return parts[0].Length >= 2
                ? parts[0][..2].ToUpperInvariant()
                : parts[0].ToUpperInvariant();
        }

        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }

    private string BuildStudentExistsFilter(string schema, string alias)
    {
        if (!_scope.ScopesEnabled || _scope.IsGlobalScope || UseSchoolWideFinance())
        {
            return string.Empty;
        }

        if (_scope.ScopeType is DataScopeType.Self or DataScopeType.LinkedStudents)
        {
            return _scope.AllowedStudentIds.Count > 0
                ? $" AND {alias}.id = ANY(@ScopeStudentIds)"
                : " AND 1 = 0";
        }

        if (_scope.AllowedClassIds.Count == 0)
        {
            return " AND 1 = 0";
        }

        return $"""
 AND EXISTS (
    SELECT 1 FROM {schema}.{DatabaseConfig.TableStudentAcademics} sa
    WHERE sa.studentid = {alias}.id
      AND sa.classid = ANY(@ScopeClassIds)
      AND {AcademicYearScopeSql.StudentAcademicEnrollmentVisibilityClause()}
)
""";
    }

    private string BuildClassFilter(string schema, string alias)
    {
        string yearFilter = AcademicYearClassFilter(alias);

        if (!_scope.ScopesEnabled || _scope.IsGlobalScope || UseSchoolWideFinance())
        {
            return yearFilter;
        }

        if (_scope.AllowedClassIds.Count == 0)
        {
            return " AND 1 = 0";
        }

        return $" AND {alias}.id = ANY(@ScopeClassIds){yearFilter}";
    }

    private static string AcademicYearClassFilter(string classTableAlias) =>
        $" AND (@ScopeAcademicYearId IS NULL OR {classTableAlias}.academicyearid = @ScopeAcademicYearId)";

    private string BuildTeacherFilter(string schema, string alias)
    {
        if (!_scope.ScopesEnabled || _scope.IsGlobalScope)
        {
            return string.Empty;
        }

        if (_scope.AllowedTeacherIds.Count > 0)
        {
            return $" AND {alias}.id = ANY(@ScopeTeacherIds)";
        }

        if (_scope.AllowedDepartmentIds.Count > 0)
        {
            return $" AND {alias}.departmentid = ANY(@ScopeDepartmentIds)";
        }

        if (_scope.ScopeType == DataScopeType.Class)
        {
            return " AND 1 = 0";
        }

        return string.Empty;
    }

    private string BuildAttendanceFilter(string schema, string alias)
    {
        string yearFilter = $"""
 AND EXISTS (
    SELECT 1 FROM {schema}.{DatabaseConfig.TableClasses} c
    WHERE c.id = {alias}.classid AND c.isactive = true
      AND (@ScopeAcademicYearId IS NULL OR c.academicyearid = @ScopeAcademicYearId)
)
""";

        if (!_scope.ScopesEnabled || _scope.IsGlobalScope)
        {
            return yearFilter;
        }

        if (_scope.ScopeType is DataScopeType.Self or DataScopeType.LinkedStudents)
        {
            return (_scope.AllowedStudentIds.Count > 0
                ? $" AND {alias}.studentid = ANY(@ScopeStudentIds)"
                : " AND 1 = 0") + yearFilter;
        }

        if (_scope.AllowedClassIds.Count == 0)
        {
            return " AND 1 = 0";
        }

        return $" AND {alias}.classid = ANY(@ScopeClassIds){yearFilter}";
    }

    private object BuildParameters(
        DashboardAttendanceDateRange? attendanceRange = null,
        DateOnly? schoolToday = null) => new
    {
        ScopeStudentIds = _scope.AllowedStudentIds.ToArray(),
        ScopeClassIds = _scope.AllowedClassIds.ToArray(),
        ScopeTeacherIds = _scope.AllowedTeacherIds.ToArray(),
        ScopeDepartmentIds = _scope.AllowedDepartmentIds.ToArray(),
        ScopeAcademicYearId = _scope.ActiveAcademicYearId,
        SchoolToday = schoolToday ?? attendanceRange?.From,
        AttendanceFromDate = attendanceRange?.From,
        AttendanceToDate = attendanceRange?.To
    };

    private sealed class DashboardRow
    {
        public int TotalStudents { get; init; }

        public int TotalTeachers { get; init; }

        public int TotalClasses { get; init; }

        public int AttendanceMarkedToday { get; init; }
    }

    private sealed class AttendanceRow
    {
        public int Present { get; init; }

        public int Absent { get; init; }

        public int Leave { get; init; }

        public int Late { get; init; }
    }

    private sealed class PayrollRunRow
    {
        public decimal TotalNet { get; init; }

        public int RunStatus { get; init; }

        public int EmployeeCount { get; init; }
    }

    private sealed class PayrollStatsRow
    {
        public int PaidCount { get; init; }

        public int PendingCount { get; init; }

        public decimal PaidAmount { get; init; }

        public decimal PendingAmount { get; init; }
    }

    private sealed class RecentStudentRow
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string ClassName { get; init; } = string.Empty;

        public int Section { get; init; }

        public DateTime CreatedOn { get; init; }
    }

    private sealed class HomeworkRow
    {
        public string Title { get; init; } = string.Empty;

        public string ClassName { get; init; } = string.Empty;

        public int Section { get; init; }

        public DateTime DueDate { get; init; }
    }

    private sealed class ClassOverviewRow
    {
        public string ClassName { get; init; } = string.Empty;

        public int Section { get; init; }

        public int StudentCount { get; init; }

        public int Present { get; init; }

        public int Late { get; init; }

        public int Absent { get; init; }

        public int OnLeave { get; init; }

        public decimal FeeCollectedToday { get; init; }
    }
}
