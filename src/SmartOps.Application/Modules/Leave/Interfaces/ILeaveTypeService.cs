using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Leave.Interfaces;

public interface ILeaveTypeService
{
    Task<Result<IList<LeaveTypeDto>>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<Result<LeaveTypeDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<LeaveTypeDto>> CreateAsync(CreateLeaveTypeDto request, CancellationToken ct = default);
    Task<Result<LeaveTypeDto>> UpdateAsync(Guid id, UpdateLeaveTypeDto request, CancellationToken ct = default);
}
