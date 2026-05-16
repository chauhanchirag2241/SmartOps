using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Domain.Modules.Teacher.Models;

namespace SmartOps.Domain.Modules.Teacher.Interfaces;

public interface ITeacherRepository
{
    Task<Guid> CreateTeacherAsync(TeacherEntity teacher, CancellationToken cancellationToken = default);
    Task<TeacherEntity?> GetTeacherByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<TeacherListModel>> GetAllTeachersAsync(
        int pageIndex,
        int pageSize,
        string? searchTerm = null,
        string? sortColumn = null,
        string? sortDirection = null,
        StaffFilter filter = StaffFilter.All,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DropdownDto>> GetClassTeacherDropdownAsync(CancellationToken cancellationToken = default);
    Task UpdateTeacherAsync(TeacherEntity teacher, CancellationToken cancellationToken = default);

    Task SetTeacherUserIdAsync(Guid teacherId, Guid userId, CancellationToken cancellationToken = default);

    Task DeleteTeacherAsync(Guid id, CancellationToken cancellationToken = default);
}
