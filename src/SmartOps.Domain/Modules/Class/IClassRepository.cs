using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Class.Entities;

namespace SmartOps.Domain.Modules.Class;

/// <summary>
/// Class persistence contract. Same shape as <see cref="SmartOps.Domain.Modules.Student.Interfaces.IStudentRepository"/>.
/// </summary>
public interface IClassRepository
{
    Task<Guid> CreateClassAsync(ClassEntity classEntity, CancellationToken cancellationToken = default);

    Task<ClassEntity?> GetClassByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false);

    Task<ClassGroupEntity?> GetClassGroupByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false);

    Task<PagedResult<ClassListModel>> GetAllClassesAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        ClassFilter filter = ClassFilter.Active,
        CancellationToken cancellationToken = default);

    /// <summary>Section-scoped dropdown (Class 1 - A).</summary>
    Task<IReadOnlyList<DropdownDto>> GetClassDropdownAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Class-group dropdown (Class 1) for fees / academic periods.</summary>
    Task<IReadOnlyList<DropdownDto>> GetClassGroupDropdownAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task UpdateClassAsync(ClassEntity classEntity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a class section; soft-deletes the group when no active sections remain.
    /// </summary>
    Task DeleteClassAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recovers a soft-deleted class section (and its group if inactive).
    /// </summary>
    Task RecoverClassAsync(Guid id, CancellationToken cancellationToken = default);
}
