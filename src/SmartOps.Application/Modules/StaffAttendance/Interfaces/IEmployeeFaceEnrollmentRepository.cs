using SmartOps.Domain.Modules.StaffAttendance.Entities;

namespace SmartOps.Application.Modules.StaffAttendance.Interfaces;

public interface IEmployeeFaceEnrollmentRepository
{
    Task<EmployeeFaceEnrollmentEntity?> GetActiveByEmployeeAsync(Guid employeeId, CancellationToken ct = default);

    Task<IList<EmployeeFaceEnrollmentEntity>> ListActiveForTenantAsync(CancellationToken ct = default);

    Task UpsertAsync(EmployeeFaceEnrollmentEntity entity, CancellationToken ct = default);

    Task DeactivateAsync(Guid employeeId, CancellationToken ct = default);
}
