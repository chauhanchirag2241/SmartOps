using SmartOps.Application.Modules.Identity.Models;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Teacher.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IUserProvisioningService
{
    /// <summary>
    /// Shared pipeline: resolve login, create or reuse user, hash default password, assign role and school mapping.
    /// </summary>
    Task<ProvisionUserResult?> ProvisionSchoolUserAsync(
        ProvisionUserRequest request,
        CancellationToken cancellationToken = default);

    Task<Guid?> ProvisionTeacherUserAsync(
        TeacherEntity teacher,
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

    /// <summary>Provision a staff persona without a dedicated domain entity (HOD, accountant, clerk, etc.).</summary>
    Task<Guid?> ProvisionStaffUserAsync(
        string email,
        string? username,
        string personaRoleLabel,
        Guid schoolId,
        DateOnly dateOfBirth,
        bool portalAccess = true,
        CancellationToken cancellationToken = default);
}
