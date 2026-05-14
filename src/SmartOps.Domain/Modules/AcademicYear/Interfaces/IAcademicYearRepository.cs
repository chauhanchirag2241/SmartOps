using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.AcademicYear.Entities;
using SmartOps.Domain.Modules.AcademicYear.Models;

namespace SmartOps.Domain.Modules.AcademicYear.Interfaces;

public interface IAcademicYearRepository
{
    Task<Guid> CreateAcademicYearAsync(AcademicYearEntity academicYear, CancellationToken cancellationToken = default);
    Task<AcademicYearEntity?> GetAcademicYearByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AcademicYearListModel>> GetAllAcademicYearsAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        AcademicYearFilter filter = AcademicYearFilter.Active,
        CancellationToken cancellationToken = default);
    Task UpdateAcademicYearAsync(AcademicYearEntity academicYear, CancellationToken cancellationToken = default);
    Task DeleteAcademicYearAsync(Guid id, CancellationToken cancellationToken = default);
}
