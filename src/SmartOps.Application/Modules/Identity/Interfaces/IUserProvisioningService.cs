using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Teacher.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IUserProvisioningService
{
    Task<Guid?> ProvisionTeacherUserAsync(
        TeacherEntity teacher,
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<Guid?> ProvisionStudentUserAsync(
        StudentEntity student,
        Guid schoolId,
        bool portalAccess,
        CancellationToken cancellationToken = default);

    Task<Guid?> ProvisionParentUserAsync(
        string email,
        string? username,
        Guid schoolId,
        CancellationToken cancellationToken = default);
}
