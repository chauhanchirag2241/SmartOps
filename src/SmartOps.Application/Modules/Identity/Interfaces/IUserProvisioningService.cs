using SmartOps.Application.Modules.Identity.Models;
using SmartOps.Domain.Modules.Employee.Entities;
using SmartOps.Domain.Modules.Student.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IUserProvisioningService
{
    Task<ProvisionUserResult?> ProvisionSchoolUserAsync(
        ProvisionUserRequest request,
        CancellationToken cancellationToken = default);

    Task<Guid?> ProvisionEmployeeUserAsync(
        EmployeeEntity employee,
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<Guid?> ProvisionStudentUserAsync(
        StudentEntity student,
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<Guid?> ProvisionParentUserAsync(
        string email,
        string? username,
        Guid schoolId,
        CancellationToken cancellationToken = default,
        DateOnly? dateOfBirth = null);

    Task<Guid?> ProvisionStaffUserAsync(
        string email,
        string? username,
        string personaRoleLabel,
        Guid schoolId,
        DateOnly dateOfBirth,
        bool portalAccess = true,
        CancellationToken cancellationToken = default);
}
