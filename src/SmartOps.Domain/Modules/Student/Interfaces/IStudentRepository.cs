using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Student.Models;

namespace SmartOps.Domain.Modules.Student.Interfaces;

public interface IStudentRepository
{
    Task<Guid> CreateStudentAsync(StudentEntity student);
    Task<StudentEntity?> GetStudentByIdAsync(Guid id);
    Task<PagedResult<StudentListModel>> GetAllStudentsAsync(int pageIndex, int pageSize, string? searchTerm = null, string? sortColumn = null, string? sortDirection = null, StudentFilter filter = StudentFilter.Active);
    Task UpdateStudentAsync(StudentEntity student);
    Task DeleteStudentAsync(Guid id);
}
