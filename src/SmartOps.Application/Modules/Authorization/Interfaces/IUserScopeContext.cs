using SmartOps.Shared.Constants;

namespace SmartOps.Application.Modules.Authorization.Interfaces;

public interface IUserScopeContext
{
    bool IsLoaded { get; }

    bool ScopesEnabled { get; }

    DataScopeType ScopeType { get; }

    bool IsGlobalScope { get; }

    int ScopeVersion { get; }

    IReadOnlyList<Guid> AllowedClassIds { get; }

    IReadOnlyList<Guid> AllowedSubjectIds { get; }

    IReadOnlyList<Guid> AllowedStudentIds { get; }

    IReadOnlyList<Guid> AllowedDepartmentIds { get; }

    IReadOnlyList<Guid> AllowedTeacherIds { get; }

    Guid? OwnStudentId { get; }

    Guid? ActiveAcademicYearId { get; }

    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

    bool HasClassAccess(Guid classId);

    bool HasSubjectAccess(Guid subjectId);

    bool HasSubjectInClassAccess(Guid classId, Guid subjectId);

    bool HasStudentAccess(Guid studentId);

    bool HasDepartmentAccess(Guid departmentId);

    bool HasTeacherAccess(Guid teacherId);
}
