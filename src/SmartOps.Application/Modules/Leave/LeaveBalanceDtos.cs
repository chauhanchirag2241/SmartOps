namespace SmartOps.Application.Modules.Leave;

public record LeaveTypeDto(
    Guid Id,
    string Code,
    string Name,
    bool IsPaid,
    bool RequiresBalance,
    bool AllowHalfDay,
    bool CarryForward,
    int SortOrder,
    bool IsActive);

public record CreateLeaveTypeDto(
    string Code,
    string Name,
    bool IsPaid = true,
    bool RequiresBalance = true,
    bool AllowHalfDay = true,
    bool CarryForward = true,
    int SortOrder = 0);

public record UpdateLeaveTypeDto(
    string Name,
    bool IsPaid,
    bool RequiresBalance,
    bool AllowHalfDay,
    bool CarryForward,
    int SortOrder,
    bool IsActive);

public record LeavePolicyDto(
    Guid Id,
    Guid UserTypeId,
    string? UserTypeName,
    Guid LeaveTypeId,
    string? LeaveTypeName,
    string? LeaveTypeCode,
    decimal MonthlyLeave);

public record UpsertLeavePolicyDto(
    Guid UserTypeId,
    Guid LeaveTypeId,
    decimal MonthlyLeave);

public record UpdateLeavePolicyMonthlyDto(decimal MonthlyLeave);

public record LeaveBalanceDto(
    Guid Id,
    Guid EmployeeId,
    string? EmployeeName,
    Guid LeaveTypeId,
    string? LeaveTypeName,
    Guid AcademicYearId,
    decimal OpeningBalance,
    decimal Accrued,
    decimal Used,
    decimal Adjusted,
    decimal ClosingBalance);

public record LeaveLedgerDto(
    Guid Id,
    Guid EmployeeId,
    Guid LeaveTypeId,
    string? LeaveTypeName,
    Guid AcademicYearId,
    short TxnType,
    string TxnTypeLabel,
    decimal Days,
    decimal BalanceAfter,
    Guid? ReferenceId,
    string? Remark,
    DateOnly TxnDate,
    DateTime CreatedOn);

public record ManualCreditLeaveDto(
    Guid EmployeeId,
    Guid LeaveTypeId,
    decimal Days,
    string? Remark);
