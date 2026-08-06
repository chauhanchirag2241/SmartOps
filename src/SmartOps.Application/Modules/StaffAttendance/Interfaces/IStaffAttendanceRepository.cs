using SmartOps.Domain.Modules.StaffAttendance;
using SmartOps.Domain.Modules.StaffAttendance.Entities;

namespace SmartOps.Application.Modules.StaffAttendance.Interfaces;

public interface IStaffAttendanceRepository
{
    Task<IList<StaffAttendanceListRow>> ListByDateAsync(DateOnly date, CancellationToken ct = default);

    Task<StaffAttendanceEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<StaffAttendanceEntity?> GetByEmployeeAndDateAsync(Guid employeeId, DateOnly date, CancellationToken ct = default);

    Task<Guid> UpsertPunchAsync(StaffAttendanceEntity entity, CancellationToken ct = default);

    Task UpdateAsync(StaffAttendanceEntity entity, CancellationToken ct = default);

    Task<Guid?> GetEmployeeIdByUserIdAsync(Guid userId, CancellationToken ct = default);

    Task<EmployeeShiftInfo?> GetEmployeeInfoAsync(Guid employeeId, CancellationToken ct = default);

    Task UpdateEmployeePhotoUrlAsync(Guid employeeId, string photoUrl, CancellationToken ct = default);

    Task<IList<StaffAttendanceReportSourceRow>> GetReportSourceAsync(
        int month,
        int year,
        Guid? departmentId,
        CancellationToken ct = default);

    Task<IList<StaffAttendanceDayStatusRow>> GetEmployeeMonthStatusesAsync(
        Guid employeeId,
        int month,
        int year,
        CancellationToken ct = default);
}

public sealed class StaffAttendanceListRow
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public DateTime? CheckInTime { get; set; }
    public DateTime? CheckOutTime { get; set; }
    public string? CheckInSource { get; set; }
    public string? CheckOutSource { get; set; }
    public StaffAttendanceStatus Status { get; set; }
    public string? Remarks { get; set; }
    public float? CheckInConfidence { get; set; }
    public float? CheckOutConfidence { get; set; }
    public bool IsFaceEnrolled { get; set; }
    public string? PhotoUrl { get; set; }
    public string? ShiftStartTime { get; set; }
    public string? ShiftEndTime { get; set; }
}

public sealed class EmployeeShiftInfo
{
    public Guid Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? ShiftStartTime { get; set; }
    public string? ShiftEndTime { get; set; }
    public string? PhotoUrl { get; set; }
    public bool IsFaceEnrolled { get; set; }
}

public sealed class StaffAttendanceReportSourceRow
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public DateOnly? AttendanceDate { get; set; }
    public StaffAttendanceStatus? Status { get; set; }
}

public sealed class StaffAttendanceDayStatusRow
{
    public DateOnly AttendanceDate { get; set; }
    public StaffAttendanceStatus Status { get; set; }
}
