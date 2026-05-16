using SmartOps.Application.Modules.Identity.DTOs;
using SmartOps.Domain.Modules.Identity.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IMenuRepository
{
    Task<IReadOnlyList<Menu>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuPermissionDto>> GetUserMenuPermissionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuPermissionDto>> GetUserMenuPermissionsForApplicationAsync(
        Guid userId,
        string application,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuDto>> GetUserMenuTreeAsync(
        Guid userId,
        string application,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleMenuPermissionDto>> GetAllMenuTemplatesAsync(CancellationToken cancellationToken = default);
}
