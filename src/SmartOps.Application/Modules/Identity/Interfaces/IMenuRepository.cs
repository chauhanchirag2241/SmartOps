using SmartOps.Application.Modules.Identity;
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

    Task<IReadOnlyList<RoleMenuPermissionDto>> GetAllMenuTemplatesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Menu permission templates for role editors, scoped to an app (plus COMMON).
    /// </summary>
    Task<IReadOnlyList<RoleMenuPermissionDto>> GetAllMenuTemplatesAsync(
        string application,
        CancellationToken cancellationToken = default);
}
