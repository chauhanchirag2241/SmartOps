namespace SmartOps.Application.Modules.Authorization.Interfaces;

public interface IScopeMappingRepository
{
    Task<Guid?> GetActiveAcademicYearIdAsync(string schema, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetTeacherClassIdsAsync(string schema, Guid userId, Guid? academicYearId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetDepartmentIdsForHodAsync(string schema, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetClassIdsByDepartmentsAsync(string schema, IReadOnlyList<Guid> departmentIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetTeacherIdsByDepartmentsAsync(string schema, IReadOnlyList<Guid> departmentIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetStudentIdsByClassIdsAsync(string schema, IReadOnlyList<Guid> classIds, Guid? academicYearId, CancellationToken cancellationToken = default);

    Task<Guid?> GetStudentIdByUserIdAsync(string schema, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetLinkedStudentIdsForParentAsync(string schema, Guid parentUserId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetStaffScopeClassIdsAsync(string schema, Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetStaffScopeDepartmentIdsAsync(string schema, Guid userId, CancellationToken cancellationToken = default);

    Task BackfillTeacherClassAssignmentsFromLegacyAsync(string schema, Guid? academicYearId, CancellationToken cancellationToken = default);

    Task UpsertTeacherClassAssignmentAsync(
        string schema,
        Guid teacherId,
        Guid classId,
        Guid academicYearId,
        bool isClassTeacher,
        CancellationToken cancellationToken = default);

    Task UpsertParentStudentMappingAsync(
        string schema,
        Guid parentUserId,
        Guid studentId,
        string relationType,
        CancellationToken cancellationToken = default);

    Task UpsertHodDepartmentAssignmentAsync(
        string schema,
        Guid userId,
        Guid departmentId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default);
}
