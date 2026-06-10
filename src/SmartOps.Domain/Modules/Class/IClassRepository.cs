using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Class.Entities;
using SmartOps.Domain.Modules.Class;

namespace SmartOps.Domain.Modules.Class;

/// <summary>
/// Class persistence contract. Same shape as <see cref="SmartOps.Domain.Modules.Student.Interfaces.IStudentRepository"/>.
/// </summary>
public interface IClassRepository
{
    Task<Guid> CreateClassAsync(ClassEntity classEntity, CancellationToken cancellationToken = default);

    Task<ClassEntity?> GetClassByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false);

    Task<PagedResult<ClassListModel>> GetAllClassesAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        ClassFilter filter = ClassFilter.Active,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DropdownDto>> GetClassDropdownAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task UpdateClassAsync(ClassEntity classEntity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft deletes a class.
    /// </summary>
    Task DeleteClassAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recovers a soft-deleted class.
    /// </summary>
    Task RecoverClassAsync(Guid id, CancellationToken cancellationToken = default);
}
