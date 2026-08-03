using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Leave.Entities;

namespace SmartOps.Application.Modules.Leave.Interfaces;

public interface ILeaveBalanceService
{
    Task<Result<IList<LeaveBalanceDto>>> GetByEmployeeAsync(Guid employeeId, Guid? academicYearId = null, CancellationToken ct = default);
    Task<Result<IList<LeaveBalanceDto>>> GetMineAsync(CancellationToken ct = default);
    Task<Result<IList<LeaveLedgerDto>>> GetLedgerAsync(Guid employeeId, Guid? leaveTypeId = null, CancellationToken ct = default);
    Task<Result<LeaveBalanceDto>> ManualCreditAsync(ManualCreditLeaveDto request, CancellationToken ct = default);
    Task<Result> DeductForApprovedLeaveAsync(LeaveRequestEntity leave, CancellationToken ct = default);
    Task<Result> ReverseForCancelledLeaveAsync(LeaveRequestEntity leave, CancellationToken ct = default);
    Task<Result> EnsureSufficientBalanceAsync(Guid employeeId, Guid leaveTypeId, decimal days, CancellationToken ct = default);
}
