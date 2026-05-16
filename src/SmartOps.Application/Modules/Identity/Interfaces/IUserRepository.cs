using SmartOps.Domain.Modules.Identity.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IUserRepository
{
    Task<ApplicationUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<ApplicationUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task CreateAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    Task UpdateAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApplicationUser>> GetUsersInRoleAsync(string roleName, CancellationToken cancellationToken = default);

    Task<IList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<(Guid RoleId, string RoleName, string RoleCode)?> GetPrimaryRoleAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddUserToRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);

    Task RemoveUserFromRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);

    Task AddUserToSchoolAsync(Guid userId, Guid schoolId, string schoolRole, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApplicationUser>> GetUsersBySchoolAsync(Guid schoolId, CancellationToken cancellationToken = default);
}
