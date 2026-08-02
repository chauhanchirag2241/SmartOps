using SmartOps.Application.Modules.Teacher;
using SmartOps.Domain.Modules.Teacher.Entities;

namespace SmartOps.Application.Modules.Teacher.Interfaces;

public interface IClassSubjectTeacherMappingRepository
{
    /// <param name="includeInactive">When true (default), returns active and inactive rows for teacher UI.</param>
    Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByEmployeeIdAsync(
        Guid employeeId,
        Guid? academicYearId,
        bool includeInactive = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByClassIdAsync(
        Guid classId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default);

    Task<ClassSubjectTeacherMappingEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Finds active or inactive row for the unique business key (for upsert/reactivate).</summary>
    Task<ClassSubjectTeacherMappingEntity?> FindByClassGroupSubjectEmployeeYearAsync(
        Guid classGroupId,
        Guid subjectId,
        Guid employeeId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    Task<ClassSubjectTeacherMappingDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsActiveClassGroupAsync(Guid classGroupId, CancellationToken cancellationToken = default);

    Task<bool> AllSubjectsBelongToClassGroupAsync(
        Guid classGroupId,
        IReadOnlyList<Guid> subjectIds,
        CancellationToken cancellationToken = default);

    Task<Guid> InsertAsync(ClassSubjectTeacherMappingEntity entity, CancellationToken cancellationToken = default);

    Task<int> UpdateAsync(ClassSubjectTeacherMappingEntity entity, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReactivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetClassIdsForTeacherUserAsync(
        Guid userId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetSubjectIdsForTeacherUserAsync(
        Guid userId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(Guid ClassId, Guid SubjectId)>> GetClassSubjectPairsForTeacherUserAsync(
        Guid userId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetSubjectIdsForClassIdsAsync(
        IReadOnlyList<Guid> classIds,
        Guid? academicYearId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassMappingSummaryDto>> GetClassSummariesAsync(
        Guid? academicYearId,
        CancellationToken cancellationToken = default);
}
