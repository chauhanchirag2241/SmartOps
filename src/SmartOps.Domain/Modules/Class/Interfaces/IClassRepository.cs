using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Class.Entities;
using SmartOps.Domain.Modules.Class.Models;

namespace SmartOps.Domain.Modules.Class.Interfaces;

/// <summary>
/// Class persistence contract. Same shape as <see cref="SmartOps.Domain.Modules.Student.Interfaces.IStudentRepository"/>.
/// </summary>
public interface IClassRepository
{
    Task<Guid> CreateClassAsync(ClassEntity classEntity, CancellationToken cancellationToken = default);

    Task<ClassEntity?> GetClassByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<ClassListModel>> GetAllClassesAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        ClassFilter filter = ClassFilter.Active,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DropdownDto>> GetClassDropdownAsync(CancellationToken cancellationToken = default);

    Task UpdateClassAsync(ClassEntity classEntity, CancellationToken cancellationToken = default);

    Task DeleteClassAsync(Guid id, CancellationToken cancellationToken = default);
}
