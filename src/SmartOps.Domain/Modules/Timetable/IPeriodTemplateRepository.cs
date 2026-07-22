using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Timetable.Entities;

namespace SmartOps.Domain.Modules.Timetable;

public interface IPeriodTemplateRepository
{
    Task<Guid> CreateTemplateAsync(
        PeriodTemplateEntity template,
        IReadOnlyList<PeriodEntity> periods,
        CancellationToken cancellationToken);

    Task UpdateTemplateAsync(
        PeriodTemplateEntity template,
        IReadOnlyList<PeriodEntity> periods,
        CancellationToken cancellationToken);

    Task DeleteTemplateAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<PeriodTemplateListModel>> GetAllTemplatesAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm,
        string? sortColumn,
        string? sortDirection,
        string? filter,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DropdownDto>> GetTemplateDropdownAsync(CancellationToken cancellationToken);

    Task<PeriodTemplateEntity?> GetTemplateByIdAsync(Guid id, CancellationToken cancellationToken, bool includeInactive = false);

    Task<IReadOnlyList<PeriodEntity>> GetPeriodsByTemplateIdAsync(Guid templateId, CancellationToken cancellationToken);
}
