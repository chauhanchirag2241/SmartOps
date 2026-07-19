using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.School.Entities;
using SmartOps.Domain.Modules.School;

namespace SmartOps.Domain.Modules.School;

public interface ISchoolRepository
{
    Task<Guid> CreateSchoolAsync(SchoolEntity school, CancellationToken cancellationToken = default);

    Task<SchoolEntity?> GetSchoolByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SchoolEntity?> GetSchoolBySubdomainAsync(string subdomain, CancellationToken cancellationToken = default);

    Task<PagedResult<SchoolListModel>> GetAllSchoolsAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        SchoolFilter filter = SchoolFilter.Active,
        CancellationToken cancellationToken = default);

    Task UpdateSchoolAsync(SchoolEntity school, CancellationToken cancellationToken = default);

    Task UpdateSchoolConnectionAsync(SchoolEntity school, CancellationToken cancellationToken = default);

    Task DeleteSchoolAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolBranchEntity>> GetBranchesAsync(Guid schoolId, CancellationToken cancellationToken = default);

    Task<SchoolBranchEntity> AddBranchAsync(
        Guid schoolId,
        string name,
        string? email,
        string? address,
        CancellationToken cancellationToken = default);

    Task<SchoolBranchEntity?> UpdateBranchAsync(
        Guid schoolId,
        Guid branchId,
        string name,
        string? email,
        string? address,
        CancellationToken cancellationToken = default);

    Task<bool> DeactivateBranchAsync(Guid schoolId, Guid branchId, CancellationToken cancellationToken = default);
}
