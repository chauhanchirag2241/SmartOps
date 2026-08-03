using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Leave.Interfaces;

public interface ILeavePolicyService
{
    Task<Result<IList<LeavePolicyDto>>> GetAllAsync(CancellationToken ct = default);
    Task<Result<LeavePolicyDto>> UpsertAsync(UpsertLeavePolicyDto request, CancellationToken ct = default);
    Task<Result<LeavePolicyDto>> UpdateMonthlyAsync(Guid id, UpdateLeavePolicyMonthlyDto request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
