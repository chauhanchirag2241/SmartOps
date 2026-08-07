using SmartOps.Domain.Modules.Leave;

namespace SmartOps.Application.Modules.Leave;

public record LeaveHalfDayDto(DateOnly Date, LeaveHalfDaySession Session);

public record CreateLeaveRequestDto(
    DateOnly FromDate,
    DateOnly ToDate,
    LeaveType? LeaveType,
    Guid? LeaveTypeId,
    string? Reason,
    bool SubmitImmediately = false,
    bool IsHalfDay = false,
    IReadOnlyList<LeaveHalfDayDto>? HalfDays = null);

public record CreateStudentLeaveRequestDto(
    Guid StudentId,
    DateOnly FromDate,
    DateOnly ToDate,
    LeaveType? LeaveType,
    string? Reason,
    bool SubmitImmediately = false);

public record LeaveListItemDto(
    Guid Id,
    LeaveRequestType RequestType,
    string RequestTypeLabel,
    Guid? EmployeeId,
    string? EmployeeName,
    Guid? StudentId,
    string? StudentName,
    string? ClassName,
    Guid RequestedByUserId,
    string? RequestedByName,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal DayCount,
    LeaveType? LeaveType,
    string? LeaveTypeLabel,
    Guid? LeaveTypeId,
    string? LeaveTypeName,
    LeaveRequestStatus Status,
    string StatusLabel,
    DateTime CreatedOn,
    bool IsHalfDay = false,
    string? Reason = null,
    string? ApprovedByName = null,
    DateTime? ApprovedOn = null);

public record LinkedStudentDto(Guid Id, string Name, string? ClassName);

public record LeaveApproverDto(Guid Id, string Name);

public record LeaveApplicantDto(
    Guid EmployeeId,
    string EmployeeName,
    LeaveApproverDto? ReportingManager);

public record LeaveDetailDto(
    Guid Id,
    LeaveRequestType RequestType,
    string RequestTypeLabel,
    Guid? EmployeeId,
    string? EmployeeName,
    Guid? StudentId,
    string? StudentName,
    string? ClassName,
    Guid RequestedByUserId,
    string? RequestedByName,
    DateOnly FromDate,
    DateOnly ToDate,
    decimal DayCount,
    LeaveType? LeaveType,
    string? LeaveTypeLabel,
    Guid? LeaveTypeId,
    string? LeaveTypeName,
    string? Reason,
    bool IsHalfDay,
    IReadOnlyList<LeaveHalfDayDto> HalfDays,
    LeaveRequestStatus Status,
    string StatusLabel,
    Guid? ApprovedByUserId,
    string? ApprovedByName,
    DateTime? ApprovedOn,
    string? ApproverRemark,
    DateTime CreatedOn);
