using SmartOps.Application.Modules.Attendance;

namespace SmartOps.Application.Modules.Attendance.Interfaces;

public interface IAttendanceReportRepository
{
    Task<AttendanceReportResponseDto> GetMonthlyAttendanceReportAsync(Guid classId, int month, Guid academicYearId, CancellationToken cancellationToken = default);
}
