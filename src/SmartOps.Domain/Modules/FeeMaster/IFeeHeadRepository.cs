using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.FeeMaster.Entities;

namespace SmartOps.Domain.Modules.FeeMaster;

public interface IFeeHeadRepository
{
    Task<PagedResult<FeeHeadListModel>> GetByFeeMasterAsync(
        Guid feeMasterId,
        int pageIndex,
        int pageSize,
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        string? filter,
        CancellationToken cancellationToken = default);

    Task<FeeHeadDetailModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false);

    Task<FeeHeadEntity?> GetEntityByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false);

    Task<Guid> CreateAsync(
        FeeHeadEntity head,
        IReadOnlyList<FeeHeadPeriodAmountEntity> periodAmounts,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        FeeHeadEntity head,
        IReadOnlyList<FeeHeadPeriodAmountEntity> periodAmounts,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
