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
    /// <param name="currentAndFutureOnly">
    /// When false, returns every active year for the year switcher.
    /// When true, keeps only current (by date) and upcoming years (excludes past).
    /// </param>
    Task<IReadOnlyList<AcademicYearDropdownItem>> GetAcademicYearDropdownAsync(
        bool currentAndFutureOnly = false,
        CancellationToken cancellationToken = default);
    Task<AcademicYearEntity?> GetCurrentAcademicYearAsync(CancellationToken cancellationToken = default);
    Task<Guid?> GetCurrentAcademicYearIdAsync(CancellationToken cancellationToken = default);
    Task<bool> AcademicYearExistsAsync(Guid id, bool requireNotDeleted = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when <paramref name="academicYearId"/> starts before <paramref name="referenceAcademicYearId"/> (a past year).
    /// </summary>
    Task<bool> IsAcademicYearBeforeAsync(
        Guid academicYearId,
        Guid referenceAcademicYearId,
        CancellationToken cancellationToken = default);
    Task UpdateAcademicYearAsync(AcademicYearEntity academicYear, CancellationToken cancellationToken = default);
    Task DeleteAcademicYearAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> TitleExistsAsync(string title, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when an active academic year already covers any day in [startDate, endDate].
    /// </summary>
    Task<bool> HasOverlappingDatesAsync(
        DateOnly startDate,
        DateOnly endDate,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default);
}
