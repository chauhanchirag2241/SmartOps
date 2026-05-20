using SmartOps.Application.Modules.Attendance;
using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Attendance.Interfaces;

public interface IAttendanceService
{
    Task<Result<ClassAttendanceResponseDto>> GetClassAttendanceAsync(
        GetClassAttendanceRequestDto request,
        CancellationToken ct = default);

    Task<Result<ClassAttendanceResponseDto>> SubmitAttendanceAsync(
        SubmitAttendanceRequestDto request,
        CancellationToken ct = default);

    Task<Result<StudentAttendanceSummaryDto>> GetStudentSummaryAsync(
        Guid studentId,
        int month,
        int year,
        CancellationToken ct = default);
}
