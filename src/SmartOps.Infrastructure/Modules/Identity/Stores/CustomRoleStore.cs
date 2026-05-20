using Microsoft.AspNetCore.Identity;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Infrastructure.Modules.Identity;

namespace SmartOps.Infrastructure.Modules.Identity.Stores;

public sealed class CustomRoleStore : IRoleStore<ApplicationRole>
{
    private readonly RoleRepository _roles;

    public CustomRoleStore(RoleRepository roles)
    {
        _roles = roles;
    }

    public void Dispose()
    {
    }

    public async Task<IdentityResult> CreateAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        await _roles.CreateAsync(role, cancellationToken).ConfigureAwait(false);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        await _roles.UpdateAsync(role, cancellationToken).ConfigureAwait(false);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        ApplicationRole? existing = await _roles.GetByIdAsync(role.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return IdentityResult.Success;
        }

        existing.IsActive = false;
        await _roles.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        return IdentityResult.Success;
    }

    public Task<string> GetRoleIdAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult(role.Id.ToString());
    }

    public Task<string?> GetRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(role.Name);
    }

    public Task SetRoleNameAsync(ApplicationRole role, string? roleName, CancellationToken cancellationToken)
    {
        role.Name = roleName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedRoleNameAsync(ApplicationRole role, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(role.Name);
    }

    public Task SetNormalizedRoleNameAsync(ApplicationRole role, string? normalizedName, CancellationToken cancellationToken)
    {
        role.Name = normalizedName ?? string.Empty;
        return Task.CompletedTask;
    }

    public async Task<ApplicationRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(roleId, out Guid id))
        {
            return null;
        }

        return await _roles.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public Task<ApplicationRole?> FindByNameAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        return _roles.GetByNameAsync(normalizedRoleName, cancellationToken);
    }
}
