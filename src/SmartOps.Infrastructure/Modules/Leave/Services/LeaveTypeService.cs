using SmartOps.Application.Modules.Leave;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common;
using SmartOps.Domain.Modules.Leave.Entities;

namespace SmartOps.Infrastructure.Modules.Leave.Services;

public sealed class LeaveTypeService : ILeaveTypeService
{
    private readonly ILeaveTypeRepository _repo;

    public LeaveTypeService(ILeaveTypeRepository repo) => _repo = repo;

    public async Task<Result<IList<LeaveTypeDto>>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        IList<LeaveTypeEntity> rows = await _repo.GetAllAsync(includeInactive, ct).ConfigureAwait(false);
        return Result<IList<LeaveTypeDto>>.Success(rows.Select(Map).ToList());
    }

    public async Task<Result<LeaveTypeDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        LeaveTypeEntity? entity = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);
        return entity is null
            ? Result<LeaveTypeDto>.Failure("Leave type not found.")
            : Result<LeaveTypeDto>.Success(Map(entity));
    }

    public async Task<Result<LeaveTypeDto>> CreateAsync(CreateLeaveTypeDto request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<LeaveTypeDto>.Failure("Code and name are required.");
        }

        string code = request.Code.Trim().ToUpperInvariant();
        if (await _repo.CodeExistsAsync(code, null, ct).ConfigureAwait(false))
        {
            return Result<LeaveTypeDto>.Failure("A leave type with this code already exists.");
        }

        var entity = new LeaveTypeEntity
        {
            Code = code,
            Name = request.Name.Trim(),
            IsPaid = request.IsPaid,
            RequiresBalance = request.RequiresBalance,
            AllowHalfDay = request.AllowHalfDay,
            CarryForward = request.CarryForward,
            SortOrder = request.SortOrder
        };

        Guid id = await _repo.CreateAsync(entity, ct).ConfigureAwait(false);
        return await GetByIdAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<Result<LeaveTypeDto>> UpdateAsync(Guid id, UpdateLeaveTypeDto request, CancellationToken ct = default)
    {
        LeaveTypeEntity? entity = await _repo.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (entity is null)
        {
            return Result<LeaveTypeDto>.Failure("Leave type not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<LeaveTypeDto>.Failure("Name is required.");
        }

        entity.Name = request.Name.Trim();
        entity.IsPaid = request.IsPaid;
        entity.RequiresBalance = request.RequiresBalance;
        entity.AllowHalfDay = request.AllowHalfDay;
        entity.CarryForward = request.CarryForward;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;

        await _repo.UpdateAsync(entity, ct).ConfigureAwait(false);
        return await GetByIdAsync(id, ct).ConfigureAwait(false);
    }

    private static LeaveTypeDto Map(LeaveTypeEntity e) =>
        new(e.Id, e.Code, e.Name, e.IsPaid, e.RequiresBalance, e.AllowHalfDay, e.CarryForward, e.SortOrder, e.IsActive);
}
