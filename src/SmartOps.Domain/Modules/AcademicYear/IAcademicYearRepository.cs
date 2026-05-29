using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.AcademicYear.Entities;
using SmartOps.Domain.Modules.AcademicYear;

namespace SmartOps.Domain.Modules.AcademicYear;

public interface IAcademicYearRepository
{
    Task<Guid> CreateAcademicYearAsync(AcademicYearEntity academicYear, CancellationToken cancellationToken = default);
    Task<AcademicYearEntity?> GetAcademicYearByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false);
    Task<PagedResult<AcademicYearListModel>> GetAllAcademicYearsAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        AcademicYearFilter filter = AcademicYearFilter.Active,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DropdownDto>> GetAcademicYearDropdownAsync(CancellationToken cancellationToken = default);
    Task UpdateAcademicYearAsync(AcademicYearEntity academicYear, CancellationToken cancellationToken = default);
    Task DeleteAcademicYearAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IList<AcademicYearSemesterEntity>> GetSemestersAsync(Guid academicYearId, CancellationToken cancellationToken = default);

    Task SaveSemestersAsync(Guid academicYearId, IList<AcademicYearSemesterInput> semesters, CancellationToken cancellationToken = default);
}

public sealed record AcademicYearSemesterInput(
    int SemesterIndex,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate);
