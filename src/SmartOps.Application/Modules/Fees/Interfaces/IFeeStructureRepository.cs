using SmartOps.Domain.Modules.Fees;

namespace SmartOps.Application.Modules.Fees.Interfaces;

public interface IFeeStructureRepository
{
    Task<IList<FeeTypeEntity>> GetFeeTypesAsync(CancellationToken ct = default);
    Task<FeeTypeEntity?> GetFeeTypeByIdAsync(Guid id, CancellationToken ct = default);
    Task<Guid> CreateFeeTypeAsync(FeeTypeEntity entity, CancellationToken ct = default);
    Task UpdateFeeTypeAsync(FeeTypeEntity entity, CancellationToken ct = default);
    Task SoftDeleteFeeTypeAsync(Guid id, CancellationToken ct = default);
    Task SetFeeTypeActiveAsync(Guid id, bool isActive, CancellationToken ct = default);
    Task<int> CountActiveFeeTypesAsync(CancellationToken ct = default);
    Task<int> CountClassesWithAmountsAsync(Guid? academicYearId, CancellationToken ct = default);
    Task<FeeSettingsEntity?> GetSettingsAsync(CancellationToken ct = default);
    Task<Guid> UpsertSettingsAsync(FeeSettingsEntity entity, CancellationToken ct = default);
}
