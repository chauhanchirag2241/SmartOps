using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Student.Models;

namespace SmartOps.Domain.Modules.Student.Interfaces;

/// <summary>
/// Student persistence contract. Use the same shape (CancellationToken last) for other module repositories.
/// </summary>
public interface IStudentRepository
{
    Task<Guid> CreateStudentAsync(StudentEntity student, CancellationToken cancellationToken = default);

    Task<StudentEntity?> GetStudentByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PagedResult<StudentListModel>> GetAllStudentsAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        StudentFilter filter = StudentFilter.Active,
        Guid? classId = null,
        CancellationToken cancellationToken = default);

    Task UpdateStudentAsync(StudentEntity student, CancellationToken cancellationToken = default);

    Task DeleteStudentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> GetMaxRollNumberAsync(Guid academicYearId, Guid classId, CancellationToken cancellationToken = default);
}

