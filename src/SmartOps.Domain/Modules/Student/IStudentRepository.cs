using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Student;

namespace SmartOps.Domain.Modules.Student;

/// <summary>
/// Student persistence contract. Use the same shape (CancellationToken last) for other module repositories.
/// </summary>
public interface IStudentRepository
{
    Task<Guid> CreateStudentAsync(StudentEntity student, CancellationToken cancellationToken = default);

    Task<StudentEntity?> GetStudentByIdAsync(Guid id, CancellationToken cancellationToken = default, bool includeInactive = false);

    Task<PagedResult<StudentListModel>> GetAllStudentsAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        StudentFilter filter = StudentFilter.Active,
        Guid? classId = null,
        IReadOnlyList<Guid>? classIds = null,
        CancellationToken cancellationToken = default);

    Task UpdateStudentAsync(StudentEntity student, CancellationToken cancellationToken = default);

    Task<bool> AdmissionNoExistsAsync(string admissionNo, Guid? excludingStudentId = null, CancellationToken cancellationToken = default);

    Task SetStudentUserIdAsync(Guid studentId, Guid userId, CancellationToken cancellationToken = default);

    Task DeleteStudentAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> GetMaxRollNumberAsync(Guid academicYearId, Guid classId, CancellationToken cancellationToken = default);

    Task<PromoteStudentsResult> PromoteStudentsAsync(
        Guid sourceAcademicYearId,
        Guid targetAcademicYearId,
        IReadOnlyList<PromoteStudentEntry> students,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns null when target year/class is ready for promotion; otherwise a user-facing message.
    /// </summary>
    Task<string?> GetPromoteTargetValidationErrorAsync(
        Guid targetAcademicYearId,
        Guid targetClassId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PromotePendingFeeRow>> GetPromotePendingFeesAsync(
        Guid sourceAcademicYearId,
        IReadOnlyList<Guid> studentIds,
        CancellationToken cancellationToken = default);
}

