using SmartOps.Shared.Constants;

namespace SmartOps.Application.Modules.Authorization.DTOs;

public sealed class UserScopeDto
{
    public DataScopeType ScopeType { get; init; }

    public int ScopeVersion { get; init; }

    public bool IsGlobalScope { get; init; }

    public IReadOnlyList<Guid> AllowedClassIds { get; init; } = [];

    /// <summary>Classes where the teacher may mark attendance (matrix permission).</summary>
    public IReadOnlyList<Guid> AllowedAttendanceClassIds { get; init; } = [];

    public IReadOnlyList<Guid> AllowedStudentIds { get; init; } = [];

    public IReadOnlyList<Guid> AllowedDepartmentIds { get; init; } = [];

    public IReadOnlyList<Guid> AllowedTeacherIds { get; init; } = [];

    public Guid? OwnStudentId { get; init; }

    public Guid? ActiveAcademicYearId { get; init; }
}
