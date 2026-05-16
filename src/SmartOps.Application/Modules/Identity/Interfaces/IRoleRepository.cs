using SmartOps.Domain.Modules.Identity.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IRoleRepository
{
    Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ApplicationRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApplicationRole?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task CreateAsync(ApplicationRole role, CancellationToken cancellationToken = default);

    Task UpdateAsync(ApplicationRole role, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetPermissionNamesForRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task SetRolePermissionsAsync(Guid roleId, IReadOnlyList<string> permissionNames, CancellationToken cancellationToken = default);
}
