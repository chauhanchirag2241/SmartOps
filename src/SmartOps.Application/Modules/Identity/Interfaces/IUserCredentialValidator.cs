using SmartOps.Domain.Modules.Identity.Entities;

namespace SmartOps.Application.Modules.Identity.Interfaces;

public interface IUserCredentialValidator
{
    Task<bool> VerifyPasswordAsync(ApplicationUser user, string plainTextPassword, CancellationToken cancellationToken = default);
}
