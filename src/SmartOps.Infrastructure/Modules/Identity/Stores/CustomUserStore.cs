using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;

namespace SmartOps.Infrastructure.Modules.Identity.Stores;

public sealed class CustomUserStore :
    IUserStore<ApplicationUser>,
    IUserPasswordStore<ApplicationUser>,
    IUserEmailStore<ApplicationUser>,
    IUserSecurityStampStore<ApplicationUser>,
    IUserLockoutStore<ApplicationUser>,
    IUserPhoneNumberStore<ApplicationUser>,
    IUserTwoFactorStore<ApplicationUser>,
    IUserClaimStore<ApplicationUser>,
    IUserRoleStore<ApplicationUser>,
    IUserAuthenticationTokenStore<ApplicationUser>,
    IUserAuthenticatorKeyStore<ApplicationUser>
{
    private readonly IUserRepository _users;

    public CustomUserStore(IUserRepository users)
    {
        _users = users;
    }

    public void Dispose()
    {
    }

    public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.Id.ToString());
    }

    public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Username);
    }

    public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
    {
        user.Username = userName ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Username);
    }

    public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
    {
        user.Username = normalizedName ?? string.Empty;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await _users.CreateAsync(user, cancellationToken).ConfigureAwait(false);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        await _users.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        ApplicationUser? existing = await _users.GetByIdAsync(user.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return IdentityResult.Success;
        }

        existing.IsActive = false;
        await _users.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
        return IdentityResult.Success;
    }

    public async Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out Guid id))
        {
            return null;
        }

        return await _users.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
    {
        return _users.GetByUsernameAsync(normalizedUserName, cancellationToken);
    }

    public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash, CancellationToken cancellationToken)
    {
        user.PasswordHash = passwordHash ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.PasswordHash);
    }

    public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(!string.IsNullOrEmpty(user.PasswordHash));
    }

    public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email);
    }

    public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        _ = confirmed;
        return Task.CompletedTask;
    }

    public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return _users.GetByEmailAsync(normalizedEmail, cancellationToken);
    }

    public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(user.Email);
    }

    public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken)
    {
        user.Email = normalizedEmail ?? string.Empty;
        return Task.CompletedTask;
    }

    public Task SetSecurityStampAsync(ApplicationUser user, string? stamp, CancellationToken cancellationToken)
    {
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.SecurityStamp);
    }

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        if (user.LockoutEnd is null)
        {
            return Task.FromResult<DateTimeOffset?>(null);
        }

        DateTime utc = DateTime.SpecifyKind(user.LockoutEnd.Value, DateTimeKind.Utc);
        return Task.FromResult<DateTimeOffset?>(new DateTimeOffset(utc));
    }

    public Task SetLockoutEndDateAsync(ApplicationUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken)
    {
        user.LockoutEnd = lockoutEnd?.UtcDateTime;
        return Task.CompletedTask;
    }

    public Task<int> IncrementAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        checked
        {
            user.AccessFailedCount += 1;
        }

        return Task.FromResult(user.AccessFailedCount);
    }

    public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        user.AccessFailedCount = 0;
        return Task.CompletedTask;
    }

    public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.AccessFailedCount);
    }

    public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.LockoutEnabled);
    }

    public Task SetLockoutEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken)
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task<string?> GetPhoneNumberAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>(string.Empty);
    }

    public Task SetPhoneNumberAsync(ApplicationUser user, string? phoneNumber, CancellationToken cancellationToken)
    {
        _ = phoneNumber;
        return Task.CompletedTask;
    }

    public Task<bool> GetPhoneNumberConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(true);
    }

    public Task SetPhoneNumberConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
    {
        _ = confirmed;
        return Task.CompletedTask;
    }

    public Task<bool> GetTwoFactorEnabledAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(false);
    }

    public Task SetTwoFactorEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken)
    {
        _ = user;
        _ = enabled;
        return Task.CompletedTask;
    }

    public Task<IList<Claim>> GetClaimsAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult<IList<Claim>>(new List<Claim>());
    }

    public Task AddClaimsAsync(ApplicationUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        _ = user;
        _ = claims;
        return Task.CompletedTask;
    }

    public Task ReplaceClaimAsync(ApplicationUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken)
    {
        _ = user;
        _ = claim;
        _ = newClaim;
        return Task.CompletedTask;
    }

    public Task RemoveClaimsAsync(ApplicationUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        _ = user;
        _ = claims;
        return Task.CompletedTask;
    }

    public async Task<IList<ApplicationUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken)
    {
        _ = claim;
        await Task.CompletedTask.ConfigureAwait(false);
        return new List<ApplicationUser>();
    }

    public async Task AddToRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken cancellationToken)
    {
        await _users.AddUserToRoleAsync(user.Id, normalizedRoleName, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveFromRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken cancellationToken)
    {
        await _users.RemoveUserFromRoleAsync(user.Id, normalizedRoleName, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IList<string>> GetRolesAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        IList<string> roles = await _users.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        return roles;
    }

    public async Task<bool> IsInRoleAsync(ApplicationUser user, string normalizedRoleName, CancellationToken cancellationToken)
    {
        IList<string> roles = await _users.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        return roles.Contains(normalizedRoleName, StringComparer.Ordinal);
    }

    public async Task<IList<ApplicationUser>> GetUsersInRoleAsync(string normalizedRoleName, CancellationToken cancellationToken)
    {
        IReadOnlyList<ApplicationUser> users = await _users.GetUsersInRoleAsync(normalizedRoleName, cancellationToken).ConfigureAwait(false);
        return users.ToList();
    }

    public Task<string?> GetTokenAsync(ApplicationUser user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        _ = user;
        _ = loginProvider;
        _ = name;
        return Task.FromResult<string?>(null);
    }

    public Task SetTokenAsync(ApplicationUser user, string loginProvider, string name, string? value, CancellationToken cancellationToken)
    {
        _ = user;
        _ = loginProvider;
        _ = name;
        _ = value;
        return Task.CompletedTask;
    }

    public Task RemoveTokenAsync(ApplicationUser user, string loginProvider, string name, CancellationToken cancellationToken)
    {
        _ = user;
        _ = loginProvider;
        _ = name;
        return Task.CompletedTask;
    }

    public Task SetAuthenticatorKeyAsync(ApplicationUser user, string key, CancellationToken cancellationToken)
    {
        _ = user;
        _ = key;
        return Task.CompletedTask;
    }

    public Task<string?> GetAuthenticatorKeyAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        _ = user;
        return Task.FromResult<string?>(null);
    }
}
