using SmartOps.Domain.Modules.School.Entities;

namespace SmartOps.Application.Abstractions;

public interface ISchoolDefaultAdminProvisioner
{
    /// <summary>
    /// Creates the default school admin user (admin / admin@smartops.com) with School Admin role.
    /// </summary>
    Task ProvisionAsync(SchoolEntity school, CancellationToken cancellationToken = default);
}
