using Microsoft.AspNetCore.Identity;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;

namespace SmartOps.Infrastructure.Modules.Identity.Services;

public sealed class UserCredentialValidator : IUserCredentialValidator
{
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

    public UserCredentialValidator(IPasswordHasher<ApplicationUser> passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    public Task<bool> VerifyPasswordAsync(ApplicationUser user, string plainTextPassword, CancellationToken cancellationToken = default)
    {
        PasswordVerificationResult result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, plainTextPassword);
        bool success = result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
        return Task.FromResult(success);
    }
}
