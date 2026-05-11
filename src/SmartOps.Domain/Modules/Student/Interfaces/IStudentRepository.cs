using SmartOps.Domain.Modules.Student.Entities;

namespace SmartOps.Domain.Modules.Student.Interfaces;

public interface IStudentRepository
{
    Task<Guid> CreateStudentAsync(StudentEntity student);
    Task<StudentEntity?> GetStudentByIdAsync(Guid id);
}
