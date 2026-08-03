using System.Data;
using SmartOps.Domain.Modules.Leave;
using SmartOps.Domain.Modules.Leave.Entities;

namespace SmartOps.Application.Modules.Leave.Interfaces;

public interface ILeaveBalanceRepository
{
    Task<IList<LeaveBalanceListRow>> GetByEmployeeAsync(Guid employeeId, Guid? academicYearId = null, CancellationToken ct = default);
    Task<LeaveBalanceEntity?> GetBalanceAsync(Guid employeeId, Guid leaveTypeId, Guid academicYearId, CancellationToken ct = default);
    Task UpsertBalanceAsync(LeaveBalanceEntity entity, CancellationToken ct = default);
    Task InsertLedgerAsync(LeaveLedgerEntity entity, CancellationToken ct = default);
    Task<IList<LeaveLedgerListRow>> GetLedgerAsync(Guid employeeId, Guid? leaveTypeId = null, CancellationToken ct = default);
    Task<Guid?> GetCurrentAcademicYearIdAsync(CancellationToken ct = default);
    Task<LeaveTypeEntity?> GetLeaveTypeAsync(Guid leaveTypeId, CancellationToken ct = default);

    // Accrual helpers — operate on an explicit school connection (cross-tenant job).
    Task<Guid?> GetCurrentAcademicYearIdAsync(IDbConnection connection, string schema, CancellationToken ct = default);
    /// <summary>Returns run id when a new run was started; null if year/month already ran.</summary>
    Task<Guid?> TryStartAccrualRunAsync(IDbConnection connection, string schema, int year, int month, CancellationToken ct = default);
    Task MarkAccrualRunAsync(
        IDbConnection connection,
        string schema,
        Guid runId,
        LeaveAccrualRunStatus status,
        int employeesScored,
        string? errorLog,
        CancellationToken ct = default);
    Task<IList<EmployeeUserTypeRow>> ListActiveEmployeesWithUserTypeAsync(
        IDbConnection connection,
        string schoolSchema,
        string identitySchema,
        CancellationToken ct = default);
    Task<IList<LeavePolicyEntity>> GetActivePoliciesAsync(IDbConnection connection, string schema, CancellationToken ct = default);
    Task ApplyAccrualCreditAsync(
        IDbConnection connection,
        string schema,
        Guid employeeId,
        Guid leaveTypeId,
        Guid academicYearId,
        decimal days,
        Guid actorId,
        DateOnly txnDate,
        string? remark,
        CancellationToken ct = default);
}

public sealed class LeaveBalanceListRow
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string? EmployeeFirstName { get; set; }
    public string? EmployeeLastName { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string? LeaveTypeName { get; set; }
    public Guid AcademicYearId { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal Accrued { get; set; }
    public decimal Used { get; set; }
    public decimal Adjusted { get; set; }
    public decimal ClosingBalance { get; set; }
}

public sealed class LeaveLedgerListRow
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LeaveTypeId { get; set; }
    public string? LeaveTypeName { get; set; }
    public Guid AcademicYearId { get; set; }
    public short TxnType { get; set; }
    public decimal Days { get; set; }
    public decimal BalanceAfter { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Remark { get; set; }
    public DateOnly TxnDate { get; set; }
    public DateTimeOffset CreatedOn { get; set; }
}

public sealed class EmployeeUserTypeRow
{
    public Guid EmployeeId { get; set; }
    public Guid UserTypeId { get; set; }
}
