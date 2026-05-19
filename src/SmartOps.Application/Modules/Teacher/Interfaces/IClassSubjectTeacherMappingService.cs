using SmartOps.Application.Modules.Teacher.DTOs;

namespace SmartOps.Application.Modules.Teacher.Interfaces;

public interface IClassSubjectTeacherMappingService
{
    Task<TeacherAssignmentsResponseDto> GetTeacherAssignmentsAsync(
        Guid teacherId,
        CancellationToken cancellationToken = default);

    Task SaveTeacherAssignmentsAsync(
        Guid teacherId,
        SaveTeacherAssignmentsRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByClassIdAsync(
        Guid classId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default);

    Task SaveClassMappingsAsync(
        Guid classId,
        SaveClassMappingsRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetBySubjectIdAsync(
        Guid subjectId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default);

    Task SaveSubjectMappingsAsync(
        Guid subjectId,
        SaveSubjectMappingsRequestDto request,
        CancellationToken cancellationToken = default);

    Task SaveSubjectTeachersAsync(
        Guid subjectId,
        SaveSubjectTeachersRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ClassSubjectTeacherMappingDto> AddMappingAsync(
        CreateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken = default);

    Task<ClassSubjectTeacherMappingDto> UpdateMappingAsync(
        Guid id,
        UpdateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken = default);

    Task RemoveMappingAsync(Guid id, CancellationToken cancellationToken = default);
}
