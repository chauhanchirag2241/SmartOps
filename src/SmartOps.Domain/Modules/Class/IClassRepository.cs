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
        Guid? classGroupId = null,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ClassGroupListModel>> GetAllClassGroupsAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        ClassFilter filter = ClassFilter.Active,
        bool scopeToActiveBranch = false,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateClassGroupAsync(ClassGroupEntity group, CancellationToken cancellationToken = default);

    Task UpdateClassGroupAsync(ClassGroupEntity group, CancellationToken cancellationToken = default);

    Task DeleteClassGroupAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ClassGroupSubjectListModel>> GetClassGroupSubjectsAsync(
        Guid classGroupId,
        CancellationToken cancellationToken = default);

    Task<Guid> AddClassGroupSubjectAsync(
        Guid classGroupId,
        Guid subjectId,
        CancellationToken cancellationToken = default);

    Task RemoveClassGroupSubjectAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Section-scoped dropdown (Class 1 - A).</summary>
    Task<IReadOnlyList<DropdownDto>> GetClassDropdownAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Subjects a user may assign for a section: class-group subjects for global scope,
    /// otherwise only subjects from classteachersubjectmappings for this class.
    /// </summary>
    Task<IReadOnlyList<DropdownDto>> GetTeachingSubjectsForClassAsync(
        Guid classId,
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Class-group dropdown (Class 1) for fees / academic periods / Add Class.</summary>
    Task<IReadOnlyList<DropdownDto>> GetClassGroupDropdownAsync(
        Guid? academicYearId = null,
        CancellationToken cancellationToken = default);

    Task UpdateClassAsync(ClassEntity classEntity, CancellationToken cancellationToken = default);

    /// <summary>Soft deletes a class section.</summary>
    Task DeleteClassAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recovers a soft-deleted class section (and its group if inactive).
    /// </summary>
    Task RecoverClassAsync(Guid id, CancellationToken cancellationToken = default);
}
