using SmartOps.Domain.Modules.Leave;

namespace SmartOps.Application.Modules.Leave;

public record CreateLeaveRequestDto(
    DateOnly FromDate,
    DateOnly ToDate,
    LeaveType? LeaveType,
    string? Reason,
    bool SubmitImmediately = false);

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
    int DayCount,
    LeaveType? LeaveType,
    string? LeaveTypeLabel,
    LeaveRequestStatus Status,
    string StatusLabel,
    DateTime CreatedOn);

public record LinkedStudentDto(Guid Id, string Name, string? ClassName);

public record LeaveApproverDto(Guid Id, string Name);

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
    int DayCount,
    LeaveType? LeaveType,
    string? LeaveTypeLabel,
    string? Reason,
    LeaveRequestStatus Status,
    string StatusLabel,
    Guid? ApprovedByUserId,
    string? ApprovedByName,
    DateTimeOffset? ApprovedOn,
    string? ApproverRemark,
    DateTime CreatedOn);
