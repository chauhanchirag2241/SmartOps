namespace SmartOps.Application.Modules.StaffAttendance.Interfaces;

public interface IEmployeeAttendanceSettingsService
{
    Task<EmployeeAttendanceTypeSettingDto> GetTypeAsync(Guid schoolId, CancellationToken ct = default);
}
