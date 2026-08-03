using Microsoft.Extensions.Logging;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Leave;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Application.Modules.Workflow.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Leave;
using SmartOps.Domain.Modules.Leave.Entities;

namespace SmartOps.Infrastructure.Modules.Leave.Services;

public sealed class LeaveService : ILeaveService
{
    private readonly ILeaveRepository _leaveRepo;
    private readonly ILeaveBalanceService _balanceService;
    private readonly ILeaveTypeRepository _leaveTypeRepo;
    private readonly IWorkflowService _workflowService;
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<LeaveService> _logger;

    public LeaveService(
        ILeaveRepository leaveRepo,
        ILeaveBalanceService balanceService,
        ILeaveTypeRepository leaveTypeRepo,
        IWorkflowService workflowService,
        ICurrentUserService currentUser,
        ITenantProvider tenantProvider,
        ILogger<LeaveService> logger)
    {
        _leaveRepo = leaveRepo;
        _balanceService = balanceService;
        _leaveTypeRepo = leaveTypeRepo;
        _workflowService = workflowService;
        _currentUser = currentUser;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    public async Task<Result<IList<LeaveApproverDto>>> GetStaffApproversAsync(CancellationToken ct = default)
    {
        if (!Guid.TryParse(_tenantProvider.GetCurrentSchoolId(), out Guid schoolId))
        {
            return Result<IList<LeaveApproverDto>>.Failure("School context is not available.");
        }

        IList<SchoolAdminUserRow> rows = await _leaveRepo.GetSchoolAdminUsersAsync(schoolId, ct).ConfigureAwait(false);
        IList<LeaveApproverDto> list = rows.Select(r => new LeaveApproverDto(r.Id, r.Name)).ToList();
        return Result<IList<LeaveApproverDto>>.Success(list);
    }

    public async Task<Result<IList<LeaveListItemDto>>> GetStaffListAsync(
        string? status, Guid? employeeid, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        IList<LeaveListRow> rows = await _leaveRepo.GetStaffListAsync(status, employeeid, from, to, ct).ConfigureAwait(false);
        return Result<IList<LeaveListItemDto>>.Success(rows.Select(MapList).ToList());
    }

    public async Task<Result<IList<LeaveListItemDto>>> GetStaffMineAsync(CancellationToken ct = default)
    {
        Guid userId = RequireUserId();
        Guid? employeeId = await _leaveRepo.GetEmployeeIdByUserIdAsync(userId, ct).ConfigureAwait(false);
        IList<LeaveListRow> rows = await _leaveRepo.GetMineAsync(LeaveRequestType.Staff, userId, ct).ConfigureAwait(false);
        if (employeeId.HasValue)
        {
            rows = rows.Where(r => r.EmployeeId == employeeId || r.RequestedByUserId == userId).ToList();
        }

        return Result<IList<LeaveListItemDto>>.Success(rows.Select(MapList).ToList());
    }

    public async Task<Result<LeaveDetailDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        LeaveDetailRow? row = await _leaveRepo.GetDetailRowAsync(id, ct).ConfigureAwait(false);
        return row is null
            ? Result<LeaveDetailDto>.Failure("Leave request not found.")
            : Result<LeaveDetailDto>.Success(MapDetail(row));
    }

    public async Task<Result<LeaveDetailDto>> CreateStaffAsync(CreateLeaveRequestDto request, CancellationToken ct = default)
    {
        var validation = ValidateDates(request.FromDate, request.ToDate);
        if (!validation.IsSuccess)
        {
            return Result<LeaveDetailDto>.Failure(validation.Error!);
        }

        Guid userId = RequireUserId();
        Guid? employeeId = await _leaveRepo.GetEmployeeIdByUserIdAsync(userId, ct).ConfigureAwait(false);
        if (!employeeId.HasValue)
        {
            return Result<LeaveDetailDto>.Failure("No teacher profile linked to your account.");
        }

        if (await _leaveRepo.HasOverlappingApprovedAsync(
                LeaveRequestType.Staff, employeeId, null, request.FromDate, request.ToDate, null, ct)
            .ConfigureAwait(false))
        {
            return Result<LeaveDetailDto>.Failure("Overlapping approved leave already exists for this period.");
        }

        Guid? leaveTypeId = request.LeaveTypeId;
        if (!leaveTypeId.HasValue && request.LeaveType == LeaveType.Casual)
        {
            leaveTypeId = LeaveTypeSeedIds.CasualLeave;
        }

        if (leaveTypeId.HasValue)
        {
            LeaveTypeEntity? leaveType = await _leaveTypeRepo.GetByIdAsync(leaveTypeId.Value, ct).ConfigureAwait(false);
            if (leaveType is null || !leaveType.IsActive)
            {
                return Result<LeaveDetailDto>.Failure("Leave type not found.");
            }
        }

        decimal totalDays = request.ToDate.DayNumber - request.FromDate.DayNumber + 1;

        var entity = new LeaveRequestEntity
        {
            RequestType = LeaveRequestType.Staff,
            EmployeeId = employeeId,
            RequestedByUserId = userId,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            LeaveType = request.LeaveType,
            LeaveTypeId = leaveTypeId,
            TotalDays = totalDays,
            IsHalfDay = false,
            DeductedFromBalance = false,
            Reason = request.Reason,
            Status = LeaveRequestStatus.Draft
        };

        Guid id = await _leaveRepo.CreateAsync(entity, ct).ConfigureAwait(false);
        if (request.SubmitImmediately)
        {
            return await SubmitInternalAsync(id, ct).ConfigureAwait(false);
        }

        return await GetByIdAsync(id, ct).ConfigureAwait(false);
    }

    public Task<Result<LeaveDetailDto>> SubmitStaffAsync(Guid id, CancellationToken ct = default) =>
        SubmitInternalAsync(id, ct);

    public async Task<Result<LeaveDetailDto>> CancelAsync(Guid id, CancellationToken ct = default)
    {
        LeaveRequestEntity? entity = await _leaveRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result<LeaveDetailDto>.Failure("Leave request not found.");
        }

        if (entity.Status is LeaveRequestStatus.Rejected or LeaveRequestStatus.Cancelled)
        {
            return Result<LeaveDetailDto>.Failure("Leave request cannot be cancelled in its current status.");
        }

        if (entity.Status == LeaveRequestStatus.Approved && entity.DeductedFromBalance)
        {
            Result reverse = await _balanceService.ReverseForCancelledLeaveAsync(entity, ct).ConfigureAwait(false);
            if (!reverse.IsSuccess)
            {
                return Result<LeaveDetailDto>.Failure(reverse.Error!);
            }

            // Reload after reverse (DeductedFromBalance updated)
            entity = await _leaveRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
            if (entity is null)
            {
                return Result<LeaveDetailDto>.Failure("Leave request not found.");
            }
        }
        else if (entity.Status is LeaveRequestStatus.Approved)
        {
            // Approved but never deducted — allow cancel without reverse
        }
        else if (entity.Status is not (LeaveRequestStatus.Draft or LeaveRequestStatus.Submitted))
        {
            return Result<LeaveDetailDto>.Failure("Leave request cannot be cancelled in its current status.");
        }

        entity.Status = LeaveRequestStatus.Cancelled;
        await _leaveRepo.UpdateAsync(entity, ct).ConfigureAwait(false);
        await _workflowService.CancelPendingForLeaveAsync(id, ct).ConfigureAwait(false);

        return await GetByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<IList<LeaveListItemDto>>> GetStudentListAsync(string? status, Guid? studentId, CancellationToken ct = default)
    {
        IList<LeaveListRow> rows = await _leaveRepo.GetStudentListAsync(status, studentId, ct).ConfigureAwait(false);
        return Result<IList<LeaveListItemDto>>.Success(rows.Select(MapList).ToList());
    }

    public async Task<Result<IList<LeaveListItemDto>>> GetStudentMineAsync(CancellationToken ct = default)
    {
        Guid userId = RequireUserId();
        IList<LeaveListRow> rows = await _leaveRepo.GetMineAsync(LeaveRequestType.Student, userId, ct).ConfigureAwait(false);
        return Result<IList<LeaveListItemDto>>.Success(rows.Select(MapList).ToList());
    }

    public async Task<Result<LeaveDetailDto>> CreateStudentAsync(CreateStudentLeaveRequestDto request, CancellationToken ct = default)
    {
        var validation = ValidateDates(request.FromDate, request.ToDate);
        if (!validation.IsSuccess)
        {
            return Result<LeaveDetailDto>.Failure(validation.Error!);
        }

        Guid userId = RequireUserId();
        if (!await _leaveRepo.IsParentLinkedToStudentAsync(userId, request.StudentId, ct).ConfigureAwait(false))
        {
            return Result<LeaveDetailDto>.Failure("You are not linked to this student.");
        }

        if (await _leaveRepo.HasOverlappingApprovedAsync(
                LeaveRequestType.Student, null, request.StudentId, request.FromDate, request.ToDate, null, ct)
            .ConfigureAwait(false))
        {
            return Result<LeaveDetailDto>.Failure("Overlapping approved leave already exists for this student.");
        }

        decimal totalDays = request.ToDate.DayNumber - request.FromDate.DayNumber + 1;

        var entity = new LeaveRequestEntity
        {
            RequestType = LeaveRequestType.Student,
            StudentId = request.StudentId,
            RequestedByUserId = userId,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            LeaveType = request.LeaveType,
            TotalDays = totalDays,
            Reason = request.Reason,
            Status = request.SubmitImmediately ? LeaveRequestStatus.Submitted : LeaveRequestStatus.Draft
        };

        Guid id = await _leaveRepo.CreateAsync(entity, ct).ConfigureAwait(false);
        if (request.SubmitImmediately)
        {
            return await SubmitInternalAsync(id, ct).ConfigureAwait(false);
        }

        return await GetByIdAsync(id, ct).ConfigureAwait(false);
    }

    public Task<Result<LeaveDetailDto>> SubmitStudentAsync(Guid id, CancellationToken ct = default) =>
        SubmitInternalAsync(id, ct);

    public async Task<Result<IList<LinkedStudentDto>>> GetLinkedStudentsForParentAsync(CancellationToken ct = default)
    {
        Guid userId = RequireUserId();
        IList<LinkedStudentRow> rows =
            await _leaveRepo.GetLinkedStudentsForParentAsync(userId, ct).ConfigureAwait(false);
        IList<LinkedStudentDto> list = rows.Select(r =>
        {
            string name = $"{r.FirstName} {r.LastName}".Trim();
            return new LinkedStudentDto(r.Id, name, r.ClassName);
        }).ToList();
        return Result<IList<LinkedStudentDto>>.Success(list);
    }

    public async Task<Result<LeaveDetailDto>> ApproveAsync(Guid leaveId, string? remark, CancellationToken ct = default)
    {
        return await SetApprovalOutcomeAsync(leaveId, LeaveRequestStatus.Approved, remark, ct).ConfigureAwait(false);
    }

    public async Task<Result<LeaveDetailDto>> RejectAsync(Guid leaveId, string? remark, CancellationToken ct = default)
    {
        return await SetApprovalOutcomeAsync(leaveId, LeaveRequestStatus.Rejected, remark, ct).ConfigureAwait(false);
    }

    private async Task<Result<LeaveDetailDto>> SubmitInternalAsync(Guid id, CancellationToken ct)
    {
        LeaveRequestEntity? entity = await _leaveRepo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result<LeaveDetailDto>.Failure("Leave request not found.");
        }

        if (entity.Status != LeaveRequestStatus.Draft)
        {
            return Result<LeaveDetailDto>.Failure("Only draft requests can be submitted.");
        }

        if (entity.RequestType == LeaveRequestType.Staff
            && entity.EmployeeId.HasValue
            && entity.LeaveTypeId.HasValue)
        {
            decimal days = entity.TotalDays > 0
                ? entity.TotalDays
                : entity.ToDate.DayNumber - entity.FromDate.DayNumber + 1;
            Result balanceCheck = await _balanceService
                .EnsureSufficientBalanceAsync(entity.EmployeeId.Value, entity.LeaveTypeId.Value, days, ct)
                .ConfigureAwait(false);
            if (!balanceCheck.IsSuccess)
            {
                return Result<LeaveDetailDto>.Failure(balanceCheck.Error!);
            }
        }

        entity.Status = LeaveRequestStatus.Submitted;
        await _leaveRepo.UpdateAsync(entity, ct).ConfigureAwait(false);

        Result workflow = await _workflowService.CreateLeaveApprovalTasksAsync(id, ct).ConfigureAwait(false);
        if (!workflow.IsSuccess)
        {
            _logger.LogWarning("Workflow creation failed for leave {Id}: {Error}", id, workflow.Error);
            return Result<LeaveDetailDto>.Failure(workflow.Error ?? "Failed to create approval tasks.");
        }

        return await GetByIdAsync(id, ct).ConfigureAwait(false);
    }

    private async Task<Result<LeaveDetailDto>> SetApprovalOutcomeAsync(
        Guid leaveId,
        LeaveRequestStatus status,
        string? remark,
        CancellationToken ct)
    {
        LeaveRequestEntity? entity = await _leaveRepo.GetByIdAsync(leaveId, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result<LeaveDetailDto>.Failure("Leave request not found.");
        }

        if (entity.Status != LeaveRequestStatus.Submitted)
        {
            return Result<LeaveDetailDto>.Failure("Only submitted requests can be approved or rejected.");
        }

        Guid userId = RequireUserId();
        if (entity.RequestedByUserId == userId)
        {
            return Result<LeaveDetailDto>.Failure("You cannot approve or reject your own leave request.");
        }

        entity.Status = status;
        entity.ApprovedByUserId = userId;
        entity.ApprovedOn = SchoolLocalTime.Now();
        entity.ApproverRemark = remark;
        await _leaveRepo.UpdateAsync(entity, ct).ConfigureAwait(false);

        if (status == LeaveRequestStatus.Approved)
        {
            Result deduct = await _balanceService.DeductForApprovedLeaveAsync(entity, ct).ConfigureAwait(false);
            if (!deduct.IsSuccess)
            {
                return Result<LeaveDetailDto>.Failure(deduct.Error!);
            }
        }

        return await GetByIdAsync(leaveId, ct).ConfigureAwait(false);
    }

    private static Result ValidateDates(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            return Result.Failure("To date cannot be before from date.");
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

    private static LeaveListItemDto MapList(LeaveListRow r)
    {
        int days = r.ToDate.DayNumber - r.FromDate.DayNumber + 1;
        string? typeLabel = r.LeaveTypeName
            ?? (r.LeaveType.HasValue ? ((LeaveType)r.LeaveType).ToString() : null);
        return new LeaveListItemDto(
            r.Id,
            (LeaveRequestType)r.RequestType,
            ((LeaveRequestType)r.RequestType).ToString(),
            r.EmployeeId,
            FormatName(r.TeacherFirstName, r.TeacherLastName),
            r.StudentId,
            FormatName(r.StudentFirstName, r.StudentLastName),
            r.ClassName,
            r.RequestedByUserId,
            r.RequestedByEmail,
            r.FromDate,
            r.ToDate,
            days,
            r.LeaveType.HasValue ? (LeaveType)r.LeaveType : null,
            typeLabel,
            r.LeaveTypeId,
            r.LeaveTypeName ?? typeLabel,
            (LeaveRequestStatus)r.Status,
            ((LeaveRequestStatus)r.Status).ToString(),
            r.CreatedOn);
    }

    private static LeaveDetailDto MapDetail(LeaveDetailRow r)
    {
        int days = r.ToDate.DayNumber - r.FromDate.DayNumber + 1;
        string? typeLabel = r.LeaveTypeName
            ?? (r.LeaveType.HasValue ? ((LeaveType)r.LeaveType).ToString() : null);
        return new LeaveDetailDto(
            r.Id,
            (LeaveRequestType)r.RequestType,
            ((LeaveRequestType)r.RequestType).ToString(),
            r.EmployeeId,
            FormatName(r.TeacherFirstName, r.TeacherLastName),
            r.StudentId,
            FormatName(r.StudentFirstName, r.StudentLastName),
            r.ClassName,
            r.RequestedByUserId,
            r.RequestedByEmail,
            r.FromDate,
            r.ToDate,
            days,
            r.LeaveType.HasValue ? (LeaveType)r.LeaveType : null,
            typeLabel,
            r.LeaveTypeId,
            r.LeaveTypeName ?? typeLabel,
            r.Reason,
            (LeaveRequestStatus)r.Status,
            ((LeaveRequestStatus)r.Status).ToString(),
            r.ApprovedByUserId,
            r.ApprovedByEmail,
            r.ApprovedOn,
            r.ApproverRemark,
            r.CreatedOn);
    }

    private static string? FormatName(string? first, string? last)
    {
        string f = first?.Trim() ?? "";
        string l = last?.Trim() ?? "";
        string combined = $"{f} {l}".Trim();
        return string.IsNullOrEmpty(combined) ? null : combined;
    }
}
