using SmartOps.Application.Modules.Teacher.DTOs;

namespace SmartOps.Application.Modules.Teacher.Interfaces;

public interface ITeacherAssignmentService
{
    Task<TeacherAssignmentsResponseDto> GetAssignmentsAsync(Guid teacherId, CancellationToken cancellationToken = default);

    Task SaveAssignmentsAsync(
        Guid teacherId,
        SaveTeacherAssignmentsRequestDto request,
        CancellationToken cancellationToken = default);
}
