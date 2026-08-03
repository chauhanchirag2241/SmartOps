using SmartOps.Domain.Common;

namespace SmartOps.Domain.Modules.Leave.Entities;

public sealed class LeaveTypeEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsPaid { get; set; } = true;
    public bool RequiresBalance { get; set; } = true;
    public bool AllowHalfDay { get; set; } = true;
    public bool CarryForward { get; set; } = true;
    public int SortOrder { get; set; }
}

public sealed class LeavePolicyEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid UserTypeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public decimal MonthlyLeave { get; set; }
}

public sealed class LeaveBalanceEntity : AuditableEntity
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public Guid AcademicYearId { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal Accrued { get; set; }
    public decimal Used { get; set; }
    public decimal Adjusted { get; set; }
    public decimal ClosingBalance { get; set; }
}

public sealed class LeaveLedgerEntity
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public Guid AcademicYearId { get; set; }
    public LeaveLedgerTxnType TxnType { get; set; }
    public decimal Days { get; set; }
    public decimal BalanceAfter { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Remark { get; set; }
    public DateOnly TxnDate { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
}

public sealed class LeaveAccrualRunEntity
{
    public Guid Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public DateTimeOffset RanOn { get; set; }
    public LeaveAccrualRunStatus Status { get; set; }
    public int EmployeesScored { get; set; }
    public string? ErrorLog { get; set; }
}
