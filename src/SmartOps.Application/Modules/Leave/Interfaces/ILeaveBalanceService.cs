using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Leave.Entities;

namespace SmartOps.Application.Modules.Leave.Interfaces;

public interface ILeaveBalanceService
{
    Task<Result<IList<LeaveBalanceDto>>> GetByEmployeeAsync(Guid employeeId, Guid? academicYearId = null, CancellationToken ct = default);
    Task<Result<IList<LeaveBalanceDto>>> GetMineAsync(CancellationToken ct = default);
    Task<Result<IList<LeaveLedgerDto>>> GetLedgerAsync(Guid employeeId, Guid? leaveTypeId = null, CancellationToken ct = default);
    Task<Result<LeaveBalanceDto>> ManualCreditAsync(ManualCreditLeaveDto request, CancellationToken ct = default);
    /// <summary>Deduct leave days from balance (on submit/pending). No-op if already deducted or type does not require balance.</summary>
    Task<Result> DeductForApprovedLeaveAsync(LeaveRequestEntity leave, string? remark = null, CancellationToken ct = default);
    /// <summary>Restore leave days to balance (on reject/cancel). No-op if not deducted.</summary>
    Task<Result> ReverseForCancelledLeaveAsync(LeaveRequestEntity leave, string? remark = null, CancellationToken ct = default);
    Task<Result> EnsureSufficientBalanceAsync(Guid employeeId, Guid leaveTypeId, decimal days, CancellationToken ct = default);
}
