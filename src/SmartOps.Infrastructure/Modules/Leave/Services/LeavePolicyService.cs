using SmartOps.Application.Modules.Leave;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.Leave.Entities;

namespace SmartOps.Infrastructure.Modules.Leave.Services;

public sealed class LeavePolicyService : ILeavePolicyService
{
    private readonly ILeavePolicyRepository _repo;
    private readonly ILeaveTypeRepository _leaveTypeRepo;

    public LeavePolicyService(ILeavePolicyRepository repo, ILeaveTypeRepository leaveTypeRepo)
    {
        _repo = repo;
        _leaveTypeRepo = leaveTypeRepo;
    }

    public async Task<Result<IList<LeavePolicyDto>>> GetAllAsync(CancellationToken ct = default)
    {
        IList<LeavePolicyListRow> rows = await _repo.GetAllAsync(ct).ConfigureAwait(false);
        IList<LeavePolicyDto> list = rows.Select(r => new LeavePolicyDto(
            r.Id,
            r.UserTypeId,
            UserTypeCodes.GetName(r.UserTypeId) ?? r.UserTypeName,
            r.LeaveTypeId,
            r.LeaveTypeName,
            r.LeaveTypeCode,
            r.MonthlyLeave)).ToList();
        return Result<IList<LeavePolicyDto>>.Success(list);
    }

    public async Task<Result<LeavePolicyDto>> UpdateMonthlyAsync(
        Guid id, UpdateLeavePolicyMonthlyDto request, CancellationToken ct = default)
    {
        if (request.MonthlyLeave < 0)
        {
            return Result<LeavePolicyDto>.Failure("Monthly leave cannot be negative.");
        }

        LeavePolicyListRow? existing = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return Result<LeavePolicyDto>.Failure("Leave policy not found.");
        }

        await _repo.UpdateMonthlyLeaveAsync(id, request.MonthlyLeave, ct).ConfigureAwait(false);
        LeavePolicyListRow? row = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (row is null)
        {
            return Result<LeavePolicyDto>.Failure("Leave policy not found after save.");
        }

        return Result<LeavePolicyDto>.Success(Map(row));
    }

    public async Task<Result<LeavePolicyDto>> UpsertAsync(UpsertLeavePolicyDto request, CancellationToken ct = default)
    {
        if (request.MonthlyLeave < 0)
        {
            return Result<LeavePolicyDto>.Failure("Monthly leave cannot be negative.");
        }

        LeaveTypeEntity? leaveType = await _leaveTypeRepo.GetByIdAsync(request.LeaveTypeId, ct).ConfigureAwait(false);
        if (leaveType is null || !leaveType.IsActive)
        {
            return Result<LeavePolicyDto>.Failure("Leave type not found.");
        }

        var entity = new LeavePolicyEntity
        {
            UserTypeId = request.UserTypeId,
            LeaveTypeId = request.LeaveTypeId,
            MonthlyLeave = request.MonthlyLeave
        };

        Guid policyId = await _repo.UpsertAsync(entity, ct).ConfigureAwait(false);
        LeavePolicyListRow? row = await _repo.GetByIdAsync(policyId, ct).ConfigureAwait(false);
        if (row is null)
        {
            return Result<LeavePolicyDto>.Failure("Leave policy not found after save.");
        }

        return Result<LeavePolicyDto>.Success(Map(row));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _repo.SoftDeleteAsync(id, ct).ConfigureAwait(false);
        return Result.Success();
    }

    private static LeavePolicyDto Map(LeavePolicyListRow row) =>
        new(
            row.Id,
            row.UserTypeId,
            UserTypeCodes.GetName(row.UserTypeId) ?? row.UserTypeName,
            row.LeaveTypeId,
            row.LeaveTypeName,
            row.LeaveTypeCode,
            row.MonthlyLeave);
}
