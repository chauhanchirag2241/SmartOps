using SmartOps.Application.Modules.Teacher.DTOs;
using SmartOps.Domain.Modules.Teacher.Entities;

namespace SmartOps.Application.Modules.Teacher.Interfaces;

public interface IClassSubjectTeacherMappingRepository
{
    Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByTeacherIdAsync(
        Guid teacherId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByClassIdAsync(
        Guid classId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default);

    Task<ClassSubjectTeacherMappingEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ClassSubjectTeacherMappingEntity?> FindByClassSubjectYearAsync(
        Guid classId,
        Guid subjectId,
        Guid academicYearId,
        CancellationToken cancellationToken = default);

    Task<ClassSubjectTeacherMappingDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid?> GetClassAcademicYearIdAsync(Guid classId, CancellationToken cancellationToken = default);

    Task<bool> ExistsActiveClassSubjectAsync(
        Guid classId,
        Guid subjectId,
        Guid academicYearId,
        Guid? excludeMappingId = null,
        CancellationToken cancellationToken = default);

    Task<Guid> InsertAsync(ClassSubjectTeacherMappingEntity entity, CancellationToken cancellationToken = default);

    Task<int> UpdateAsync(ClassSubjectTeacherMappingEntity entity, CancellationToken cancellationToken = default);

    Task<bool> SetClassTeacherFlagAsync(
        Guid mappingId,
        Guid classId,
        Guid academicYearId,
        bool isClassTeacher,
        CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task ClearClassTeacherFlagAsync(Guid classId, Guid academicYearId, CancellationToken cancellationToken = default);

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
