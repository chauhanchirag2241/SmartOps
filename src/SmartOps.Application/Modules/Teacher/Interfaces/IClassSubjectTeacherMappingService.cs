using SmartOps.Application.Modules.Teacher;

namespace SmartOps.Application.Modules.Teacher.Interfaces;

public interface IClassSubjectTeacherMappingService
{
    Task<MappingLookupsResponseDto> GetLookupsAsync(
        Guid? academicYearId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassSubjectTeacherMappingDto>> GetByClassIdAsync(
        Guid classId,
        Guid? academicYearId,
        CancellationToken cancellationToken = default);

    Task<ClassSubjectTeacherMappingDto> AddMappingAsync(
        CreateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken = default);

    Task<ClassSubjectTeacherMappingDto> UpdateMappingAsync(
        Guid id,
        UpdateClassSubjectTeacherMappingDto request,
        CancellationToken cancellationToken = default);

    Task<ClassSubjectTeacherMappingDto> SetClassTeacherAsync(
        Guid id,
        bool isClassTeacher,
        CancellationToken cancellationToken = default);

    Task<ClassSubjectTeacherMappingDto> AssignTeacherLaterAsync(
        Guid id,
        AssignTeacherLaterRequestDto request,
        CancellationToken cancellationToken = default);

    Task RemoveMappingAsync(Guid id, CancellationToken cancellationToken = default);
}
