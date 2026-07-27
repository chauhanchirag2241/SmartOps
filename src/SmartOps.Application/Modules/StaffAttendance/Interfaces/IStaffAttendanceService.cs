using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.StaffAttendance.Interfaces;

public interface IStaffAttendanceService
{
    Task<Result<EmployeeAttendanceTypeSettingDto>> GetSettingsAsync(CancellationToken ct = default);

    Task<Result<IList<StaffAttendanceRowDto>>> ListByDateAsync(DateOnly date, CancellationToken ct = default);

    Task<Result<StaffAttendanceRowDto?>> GetMyTodayAsync(CancellationToken ct = default);

    Task<Result<StaffAttendanceRowDto>> ManualPunchAsync(ManualPunchRequestDto request, CancellationToken ct = default);

    Task<Result<StaffAttendanceRowDto>> UpdateAsync(Guid id, UpdateStaffAttendanceRequestDto request, CancellationToken ct = default);

    Task<Result> EnrollFaceAsync(
        Guid? employeeId,
        byte[] imageBytes,
        string contentType,
        string? fileName,
        CancellationToken ct = default);

    Task<Result<StaffAttendanceRowDto>> FacePunchAsync(byte[] imageBytes, CancellationToken ct = default);

    Task<Result> DeactivateFaceEnrollmentAsync(Guid employeeId, CancellationToken ct = default);

    Task<Result<StaffAttendanceReportDto>> GetReportAsync(
        int month,
        int year,
        Guid? departmentId,
        CancellationToken ct = default);
}
