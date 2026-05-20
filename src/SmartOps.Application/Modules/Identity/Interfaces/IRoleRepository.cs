using SmartOps.Application.Modules.Identity;
using SmartOps.Domain.Modules.Identity.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IRoleRepository
{
    Task<ApplicationRole?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApplicationRole?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task CreateAsync(ApplicationRole role, CancellationToken cancellationToken = default);

    Task UpdateAsync(ApplicationRole role, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApplicationRole>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleMenuPermissionDto>> GetMenuPermissionsForRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task SetRoleMenuPermissionsAsync(Guid roleId, IReadOnlyList<RoleMenuPermissionDto> permissions, CancellationToken cancellationToken = default);
}
