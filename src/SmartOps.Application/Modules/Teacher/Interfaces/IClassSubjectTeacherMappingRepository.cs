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

    Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetBySubjectIdAsync(
        Guid subjectId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default);

    Task<ClassSubjectTeacherMappingEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Guid> InsertAsync(ClassSubjectTeacherMappingEntity entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(ClassSubjectTeacherMappingEntity entity, CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task SoftDeleteByClassAsync(Guid classId, Guid academicYearId, CancellationToken cancellationToken = default);

    Task SoftDeleteBySubjectAsync(Guid subjectId, Guid academicYearId, CancellationToken cancellationToken = default);

    Task SoftDeleteByTeacherAsync(Guid teacherId, Guid academicYearId, CancellationToken cancellationToken = default);

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
}
