using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Timetable.Entities;

namespace SmartOps.Domain.Modules.Timetable;

public interface IPeriodRepository
{
    Task<Guid> CreatePeriodAsync(PeriodEntity period, CancellationToken cancellationToken);
    Task<PagedResult<PeriodListModel>> GetAllPeriodsAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        string? filter,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<DropdownDto>> GetPeriodDropdownAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PeriodEntity>> GetActivePeriodsOrderedAsync(CancellationToken cancellationToken);
    Task<PeriodEntity?> GetPeriodByIdAsync(Guid id, CancellationToken cancellationToken, bool includeInactive = false);
    Task UpdatePeriodAsync(PeriodEntity period, CancellationToken cancellationToken);
    Task DeletePeriodAsync(Guid id, CancellationToken cancellationToken);
}
