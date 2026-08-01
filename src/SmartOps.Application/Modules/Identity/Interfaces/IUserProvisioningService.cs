using System.Data;
using SmartOps.Application.Modules.Identity.Models;
using SmartOps.Domain.Modules.Employee.Entities;
using SmartOps.Domain.Modules.Student.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IUserProvisioningService
{
    Task<ProvisionUserResult> ProvisionSchoolUserAsync(
        ProvisionUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Same as <see cref="ProvisionSchoolUserAsync(ProvisionUserRequest, CancellationToken)"/> using an ambient transaction.</summary>
    Task<ProvisionUserResult> ProvisionSchoolUserAsync(
        ProvisionUserRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<Guid> ProvisionEmployeeUserAsync(
        EmployeeEntity employee,
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<Guid> ProvisionEmployeeUserAsync(
        EmployeeEntity employee,
        Guid schoolId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<Guid> ProvisionStudentUserAsync(
        StudentEntity student,
        Guid schoolId,
        CancellationToken cancellationToken = default);

    Task<Guid> ProvisionStudentUserAsync(
        StudentEntity student,
        Guid schoolId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<Guid?> ProvisionStaffUserAsync(
        string email,
        string? username,
        string firstName,
        string lastName,
        string? mobile,
        string personaRoleLabel,
        string userTypeCode,
        Guid schoolId,
        CancellationToken cancellationToken = default);
}
