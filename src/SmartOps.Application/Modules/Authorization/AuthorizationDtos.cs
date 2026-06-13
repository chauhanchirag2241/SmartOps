using SmartOps.Domain.Common.Enums;

namespace SmartOps.Application.Modules.Authorization;

public sealed class DashboardSummaryDto
{
    public int TotalStudents { get; init; }

    public int TotalEmployees { get; init; }

    public int TotalClasses { get; init; }

    public int AttendanceMarkedToday { get; init; }

    public double AverageAttendancePercent { get; init; }

    public string ScopeLabel { get; init; } = string.Empty;
}

public sealed class AssignParentStudentDto
{
    public string ParentEmail { get; set; } = null!;

    public string? ParentUsername { get; set; }

    public Guid StudentId { get; set; }

    public string RelationType { get; set; } = "Parent";
}

public sealed class AssignHodDepartmentDto
{
    public Guid UserId { get; set; }

    public Guid DepartmentId { get; set; }

    public Guid? AcademicYearId { get; set; }
}

public sealed class UserScopeDto
{
    public DataScopeType ScopeType { get; init; }

    public int ScopeVersion { get; init; }

    public bool IsGlobalScope { get; init; }

    public IReadOnlyList<Guid> AllowedClassIds { get; init; } = [];

    public IReadOnlyList<Guid> AllowedSubjectIds { get; init; } = [];

    public IReadOnlyList<Guid> AllowedStudentIds { get; init; } = [];

    public IReadOnlyList<Guid> AllowedDepartmentIds { get; init; } = [];

    public IReadOnlyList<Guid> AllowedEmployeeIds { get; init; } = [];

    public Guid? OwnStudentId { get; init; }

    public Guid? ActiveAcademicYearId { get; init; }
}