using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Leave;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Modules.Leave;
using SmartOps.Domain.Modules.Leave.Entities;

namespace SmartOps.Infrastructure.Modules.Leave.Services;

public sealed class LeaveBalanceService : ILeaveBalanceService
{
    private readonly ILeaveBalanceRepository _balanceRepo;
    private readonly ILeaveRepository _leaveRepo;
    private readonly ICurrentUserService _currentUser;

    public LeaveBalanceService(
        ILeaveBalanceRepository balanceRepo,
        ILeaveRepository leaveRepo,
        ICurrentUserService currentUser)
    {
        _balanceRepo = balanceRepo;
        _leaveRepo = leaveRepo;
        _currentUser = currentUser;
    }

    public async Task<Result<IList<LeaveBalanceDto>>> GetByEmployeeAsync(
        Guid employeeId, Guid? academicYearId = null, CancellationToken ct = default)
    {
        IList<LeaveBalanceListRow> rows = await _balanceRepo.GetByEmployeeAsync(employeeId, academicYearId, ct)
            .ConfigureAwait(false);
        return Result<IList<LeaveBalanceDto>>.Success(rows.Select(MapBalance).ToList());
    }

    public async Task<Result<IList<LeaveBalanceDto>>> GetMineAsync(CancellationToken ct = default)
    {
        Guid userId = RequireUserId();
        Guid? employeeId = await _leaveRepo.GetEmployeeIdByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (!employeeId.HasValue)
        {
            return Result<IList<LeaveBalanceDto>>.Failure("No employee profile linked to your account.");
        }

        return await GetByEmployeeAsync(employeeId.Value, null, ct).ConfigureAwait(false);
    }

    public async Task<Result<IList<LeaveLedgerDto>>> GetLedgerAsync(
        Guid employeeId, Guid? leaveTypeId = null, CancellationToken ct = default)
    {
        IList<LeaveLedgerListRow> rows = await _balanceRepo.GetLedgerAsync(employeeId, leaveTypeId, ct)
            .ConfigureAwait(false);
        return Result<IList<LeaveLedgerDto>>.Success(rows.Select(MapLedger).ToList());
    }

    public async Task<Result<LeaveBalanceDto>> ManualCreditAsync(ManualCreditLeaveDto request, CancellationToken ct = default)
    {
        if (request.Days == 0)
        {
            return Result<LeaveBalanceDto>.Failure("Days must be non-zero.");
        }

        LeaveTypeEntity? leaveType = await _balanceRepo.GetLeaveTypeAsync(request.LeaveTypeId, ct).ConfigureAwait(false);
        if (leaveType is null)
        {
            return Result<LeaveBalanceDto>.Failure("Leave type not found.");
        }

        Guid? academicYearId = await _balanceRepo.GetCurrentAcademicYearIdAsync(ct).ConfigureAwait(false);
        if (!academicYearId.HasValue)
        {
            return Result<LeaveBalanceDto>.Failure("No current academic year found.");
        }

        Guid actorId = RequireUserId();
        LeaveBalanceEntity? balance = await _balanceRepo
            .GetBalanceAsync(request.EmployeeId, request.LeaveTypeId, academicYearId.Value, ct)
            .ConfigureAwait(false);

        if (balance is null)
        {
            balance = new LeaveBalanceEntity
            {
                Id = Guid.NewGuid(),
                EmployeeId = request.EmployeeId,
                LeaveTypeId = request.LeaveTypeId,
                AcademicYearId = academicYearId.Value,
                OpeningBalance = 0,
                Accrued = 0,
                Used = 0,
                Adjusted = request.Days,
                ClosingBalance = request.Days
            };
        }
        else
        {
            balance.Adjusted += request.Days;
            balance.ClosingBalance = balance.OpeningBalance + balance.Accrued - balance.Used + balance.Adjusted;
        }

        if (balance.ClosingBalance < 0)
        {
            return Result<LeaveBalanceDto>.Failure("Adjustment would result in a negative balance.");
        }

        await _balanceRepo.UpsertBalanceAsync(balance, ct).ConfigureAwait(false);

        await _balanceRepo.InsertLedgerAsync(new LeaveLedgerEntity
        {
            EmployeeId = request.EmployeeId,
            LeaveTypeId = request.LeaveTypeId,
            AcademicYearId = academicYearId.Value,
            TxnType = LeaveLedgerTxnType.ManualAdjust,
            Days = request.Days,
            BalanceAfter = balance.ClosingBalance,
            Remark = request.Remark,
            TxnDate = SchoolLocalTime.Today(null),
            CreatedBy = actorId,
            CreatedOn = SchoolLocalTime.Now()
        }, ct).ConfigureAwait(false);

        IList<LeaveBalanceListRow> rows = await _balanceRepo
            .GetByEmployeeAsync(request.EmployeeId, academicYearId, ct)
            .ConfigureAwait(false);
        LeaveBalanceListRow? row = rows.FirstOrDefault(r => r.LeaveTypeId == request.LeaveTypeId);
        return row is null
            ? Result<LeaveBalanceDto>.Failure("Balance not found after credit.")
            : Result<LeaveBalanceDto>.Success(MapBalance(row));
    }

    public async Task<Result> DeductForApprovedLeaveAsync(LeaveRequestEntity leave, CancellationToken ct = default)
    {
        if (leave.RequestType != LeaveRequestType.Staff
            || !leave.EmployeeId.HasValue
            || !leave.LeaveTypeId.HasValue
            || leave.DeductedFromBalance)
        {
            return Result.Success();
        }

        LeaveTypeEntity? leaveType = await _balanceRepo.GetLeaveTypeAsync(leave.LeaveTypeId.Value, ct)
            .ConfigureAwait(false);
        if (leaveType is null || !leaveType.RequiresBalance)
        {
            return Result.Success();
        }

        decimal days = leave.TotalDays > 0
            ? leave.TotalDays
            : leave.ToDate.DayNumber - leave.FromDate.DayNumber + 1;

        Guid? academicYearId = await _balanceRepo.GetCurrentAcademicYearIdAsync(ct).ConfigureAwait(false);
        if (!academicYearId.HasValue)
        {
            return Result.Failure("No current academic year found for leave deduction.");
        }

        LeaveBalanceEntity? balance = await _balanceRepo
            .GetBalanceAsync(leave.EmployeeId.Value, leave.LeaveTypeId.Value, academicYearId.Value, ct)
            .ConfigureAwait(false);

        if (balance is null || balance.ClosingBalance < days)
        {
            return Result.Failure("Insufficient leave balance.");
        }

        balance.Used += days;
        balance.ClosingBalance = balance.OpeningBalance + balance.Accrued - balance.Used + balance.Adjusted;
        await _balanceRepo.UpsertBalanceAsync(balance, ct).ConfigureAwait(false);

        Guid actorId = leave.ApprovedByUserId ?? Guid.Parse(DatabaseConfig.SystemUserId);
        await _balanceRepo.InsertLedgerAsync(new LeaveLedgerEntity
        {
            EmployeeId = leave.EmployeeId.Value,
            LeaveTypeId = leave.LeaveTypeId.Value,
            AcademicYearId = academicYearId.Value,
            TxnType = LeaveLedgerTxnType.Usage,
            Days = -days,
            BalanceAfter = balance.ClosingBalance,
            ReferenceId = leave.Id,
            Remark = "Leave approved",
            TxnDate = SchoolLocalTime.Today(null),
            CreatedBy = actorId,
            CreatedOn = SchoolLocalTime.Now()
        }, ct).ConfigureAwait(false);

        leave.DeductedFromBalance = true;
        await _leaveRepo.UpdateAsync(leave, ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> ReverseForCancelledLeaveAsync(LeaveRequestEntity leave, CancellationToken ct = default)
    {
        if (!leave.DeductedFromBalance
            || !leave.EmployeeId.HasValue
            || !leave.LeaveTypeId.HasValue)
        {
            return Result.Success();
        }

        decimal days = leave.TotalDays > 0
            ? leave.TotalDays
            : leave.ToDate.DayNumber - leave.FromDate.DayNumber + 1;

        Guid? academicYearId = await _balanceRepo.GetCurrentAcademicYearIdAsync(ct).ConfigureAwait(false);
        if (!academicYearId.HasValue)
        {
            return Result.Failure("No current academic year found for leave reversal.");
        }

        LeaveBalanceEntity? balance = await _balanceRepo
            .GetBalanceAsync(leave.EmployeeId.Value, leave.LeaveTypeId.Value, academicYearId.Value, ct)
            .ConfigureAwait(false);

        if (balance is null)
        {
            balance = new LeaveBalanceEntity
            {
                Id = Guid.NewGuid(),
                EmployeeId = leave.EmployeeId.Value,
                LeaveTypeId = leave.LeaveTypeId.Value,
                AcademicYearId = academicYearId.Value,
                OpeningBalance = 0,
                Accrued = 0,
                Used = 0,
                Adjusted = days,
                ClosingBalance = days
            };
        }
        else
        {
            balance.Used = Math.Max(0, balance.Used - days);
            balance.ClosingBalance = balance.OpeningBalance + balance.Accrued - balance.Used + balance.Adjusted;
        }

        await _balanceRepo.UpsertBalanceAsync(balance, ct).ConfigureAwait(false);

        Guid actorId = _currentUser.IsAuthenticated && _currentUser.UserId != Guid.Empty
            ? _currentUser.UserId
            : Guid.Parse(DatabaseConfig.SystemUserId);

        await _balanceRepo.InsertLedgerAsync(new LeaveLedgerEntity
        {
            EmployeeId = leave.EmployeeId.Value,
            LeaveTypeId = leave.LeaveTypeId.Value,
            AcademicYearId = academicYearId.Value,
            TxnType = LeaveLedgerTxnType.Reverse,
            Days = days,
            BalanceAfter = balance.ClosingBalance,
            ReferenceId = leave.Id,
            Remark = "Leave cancelled — balance restored",
            TxnDate = SchoolLocalTime.Today(null),
            CreatedBy = actorId,
            CreatedOn = SchoolLocalTime.Now()
        }, ct).ConfigureAwait(false);

        leave.DeductedFromBalance = false;
        await _leaveRepo.UpdateAsync(leave, ct).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result> EnsureSufficientBalanceAsync(
        Guid employeeId, Guid leaveTypeId, decimal days, CancellationToken ct = default)
    {
        LeaveTypeEntity? leaveType = await _balanceRepo.GetLeaveTypeAsync(leaveTypeId, ct).ConfigureAwait(false);
        if (leaveType is null)
        {
            return Result.Failure("Leave type not found.");
        }

        if (!leaveType.RequiresBalance)
        {
            return Result.Success();
        }

        Guid? academicYearId = await _balanceRepo.GetCurrentAcademicYearIdAsync(ct).ConfigureAwait(false);
        if (!academicYearId.HasValue)
        {
            return Result.Failure("No current academic year found.");
        }

        LeaveBalanceEntity? balance = await _balanceRepo
            .GetBalanceAsync(employeeId, leaveTypeId, academicYearId.Value, ct)
            .ConfigureAwait(false);

        decimal available = balance?.ClosingBalance ?? 0;
        if (available < days)
        {
            return Result.Failure($"Insufficient leave balance. Available: {available}, requested: {days}.");
        }

        return Result.Success();
    }

    private Guid RequireUserId()
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("User is not authenticated.");
        }

        return _currentUser.UserId;
    }

    private static LeaveBalanceDto MapBalance(LeaveBalanceListRow r)
    {
        string? name = $"{r.EmployeeFirstName} {r.EmployeeLastName}".Trim();
        return new LeaveBalanceDto(
            r.Id,
            r.EmployeeId,
            string.IsNullOrEmpty(name) ? null : name,
            r.LeaveTypeId,
            r.LeaveTypeName,
            r.AcademicYearId,
            r.OpeningBalance,
            r.Accrued,
            r.Used,
            r.Adjusted,
            r.ClosingBalance);
    }

    private static LeaveLedgerDto MapLedger(LeaveLedgerListRow r)
    {
        var txn = (LeaveLedgerTxnType)r.TxnType;
        return new LeaveLedgerDto(
            r.Id,
            r.EmployeeId,
            r.LeaveTypeId,
            r.LeaveTypeName,
            r.AcademicYearId,
            r.TxnType,
            txn.ToString(),
            r.Days,
            r.BalanceAfter,
            r.ReferenceId,
            r.Remark,
            r.TxnDate,
            r.CreatedOn);
    }
}
