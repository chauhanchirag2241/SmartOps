using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.School.Entities;
using SmartOps.Domain.Modules.School.Models;

namespace SmartOps.Domain.Modules.School.Interfaces;

public interface ISchoolRepository
{
    Task<Guid> CreateSchoolAsync(SchoolEntity school, CancellationToken cancellationToken = default);

    Task<SchoolEntity?> GetSchoolByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<SchoolListModel>> GetAllSchoolsAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        SchoolFilter filter = SchoolFilter.Active,
        CancellationToken cancellationToken = default);

    Task UpdateSchoolAsync(SchoolEntity school, CancellationToken cancellationToken = default);

    Task DeleteSchoolAsync(Guid id, CancellationToken cancellationToken = default);
}
