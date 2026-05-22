using SmartOps.Domain.Common;

namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IFeeStructureService
{
    Task<Result<IList<FeeTypeDto>>> GetFeeTypesAsync(CancellationToken ct = default);
    Task<Result<FeeStructureStatsDto>> GetStatsAsync(CancellationToken ct = default);
    Task<Result<FeeSettingsDto>> GetSettingsAsync(CancellationToken ct = default);
    Task<Result<FeeSettingsDto>> UpsertSettingsAsync(UpsertFeeSettingsRequestDto request, CancellationToken ct = default);
    Task<Result<FeeTypeDto>> CreateFeeTypeAsync(CreateFeeTypeRequestDto request, CancellationToken ct = default);
    Task<Result<FeeTypeDto>> UpdateFeeTypeAsync(Guid id, UpdateFeeTypeRequestDto request, CancellationToken ct = default);
    Task<Result<bool>> DeleteFeeTypeAsync(Guid id, CancellationToken ct = default);
    Task<Result<FeeTypeDto>> SetActiveAsync(Guid id, ToggleFeeTypeActiveRequestDto request, CancellationToken ct = default);
}
