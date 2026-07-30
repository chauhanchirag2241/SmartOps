using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.FeeMaster.Entities;

namespace SmartOps.Domain.Modules.FeeMaster;

public interface IFeeMasterRepository
{
    Task<Guid> CreateAsync(FeeMasterEntity fee, CancellationToken cancellationToken = default);

    Task<PagedResult<FeeMasterListModel>> GetAllAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        string? filter,
        CancellationToken cancellationToken = default);

    Task<FeeMasterEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false);

    Task UpdateAsync(FeeMasterEntity fee, CancellationToken cancellationToken = default);

    Task UpdateBasicAsync(FeeMasterEntity fee, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetClassGroupIdsAsync(Guid feeMasterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces class-group links. When <paramref name="allowRemove"/> is false,
    /// existing links are kept and only new ids are inserted.
    /// </summary>
    Task SaveClassGroupIdsAsync(
        Guid feeMasterId,
        Guid branchId,
        IReadOnlyList<Guid> classGroupIds,
        bool allowRemove,
        CancellationToken cancellationToken = default);
}
