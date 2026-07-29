using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Shift.Entities;

namespace SmartOps.Domain.Modules.Shift;

public interface IShiftRepository
{
    Task<Guid> CreateAsync(ShiftEntity shift, CancellationToken cancellationToken = default);

    Task<PagedResult<ShiftListModel>> GetAllAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        string? filter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DropdownDto>> GetDropdownAsync(CancellationToken cancellationToken = default);

    Task<ShiftEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false);

    Task UpdateAsync(ShiftEntity shift, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
