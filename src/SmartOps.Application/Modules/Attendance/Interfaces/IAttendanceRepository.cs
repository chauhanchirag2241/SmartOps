using AttendanceEntity = SmartOps.Domain.Modules.Attendance.Entities.Attendance;

namespace SmartOps.Application.Modules.Attendance.Interfaces;

public interface IAttendanceRepository
{
    Task<IList<AttendanceEntity>> GetByClassAndDateAsync(
        Guid classId,
        DateOnly date,
        CancellationToken ct = default);

    Task<AttendanceEntity?> GetByStudentAndDateAsync(
        Guid studentId,
        DateOnly date,
        CancellationToken ct = default);

    Task<IList<AttendanceEntity>> GetByStudentAndRangeAsync(
        Guid studentId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);

    Task UpsertAsync(AttendanceEntity attendance, CancellationToken ct = default);

    Task BulkUpsertAsync(
        IList<AttendanceEntity> records,
        CancellationToken ct = default);

    Task<bool> IsSubmittedAsync(
        Guid classId,
        DateOnly date,
        CancellationToken ct = default);
}
