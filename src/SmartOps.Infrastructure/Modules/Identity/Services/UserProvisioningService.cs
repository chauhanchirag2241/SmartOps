using Microsoft.AspNetCore.Identity;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Shared.Constants;

namespace SmartOps.Infrastructure.Modules.Identity.Services;

public sealed class UserProvisioningService : IUserProvisioningService
{
    private const string DefaultPassword = "ChangeMe@123";
    private readonly IUserRepository _users;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

    public UserProvisioningService(IUserRepository users, IPasswordHasher<ApplicationUser> passwordHasher)
    {
        _users = users;
        _passwordHasher = passwordHasher;
    }

    public async Task<Guid?> ProvisionTeacherUserAsync(
        TeacherEntity teacher,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        if (!teacher.PortalAccess || string.IsNullOrWhiteSpace(teacher.Email))
        {
            return null;
        }

        string username = !string.IsNullOrWhiteSpace(teacher.Username)
            ? teacher.Username.Trim()
            : teacher.Email.Trim().ToLowerInvariant();

        ApplicationUser? existing = await _users.GetByEmailAsync(teacher.Email.Trim(), cancellationToken).ConfigureAwait(false)
            ?? await _users.GetByUsernameAsync(username, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            await _users.AddUserToRoleAsync(existing.Id, RoleNames.Teacher, cancellationToken).ConfigureAwait(false);
            await _users.AddUserToSchoolAsync(existing.Id, schoolId, RoleNames.Teacher, cancellationToken).ConfigureAwait(false);
            return existing.Id;
        }

        var user = new ApplicationUser
        {
            Username = username,
            Email = teacher.Email.Trim().ToLowerInvariant(),
            IsActive = true,
            LockoutEnabled = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, DefaultPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        await _users.CreateAsync(user, cancellationToken).ConfigureAwait(false);
        await _users.AddUserToRoleAsync(user.Id, RoleNames.Teacher, cancellationToken).ConfigureAwait(false);
        await _users.AddUserToSchoolAsync(user.Id, schoolId, RoleNames.Teacher, cancellationToken).ConfigureAwait(false);

        return user.Id;
    }

    public async Task<Guid?> ProvisionStudentUserAsync(
        StudentEntity student,
        Guid schoolId,
        bool portalAccess,
        CancellationToken cancellationToken = default)
    {
        if (!portalAccess || string.IsNullOrWhiteSpace(student.Email))
        {
            return null;
        }

        string username = student.Email.Trim().ToLowerInvariant();
        ApplicationUser? existing = await _users.GetByEmailAsync(student.Email.Trim(), cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            await _users.AddUserToRoleAsync(existing.Id, RoleNames.Student, cancellationToken).ConfigureAwait(false);
            await _users.AddUserToSchoolAsync(existing.Id, schoolId, RoleNames.Student, cancellationToken).ConfigureAwait(false);
            return existing.Id;
        }

        var user = new ApplicationUser
        {
            Username = username,
            Email = student.Email.Trim().ToLowerInvariant(),
            IsActive = true,
            LockoutEnabled = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, DefaultPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        await _users.CreateAsync(user, cancellationToken).ConfigureAwait(false);
        await _users.AddUserToRoleAsync(user.Id, RoleNames.Student, cancellationToken).ConfigureAwait(false);
        await _users.AddUserToSchoolAsync(user.Id, schoolId, RoleNames.Student, cancellationToken).ConfigureAwait(false);

        return user.Id;
    }

    public async Task<Guid?> ProvisionParentUserAsync(
        string email,
        string? username,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        string normalizedEmail = email.Trim().ToLowerInvariant();
        string loginName = !string.IsNullOrWhiteSpace(username)
            ? username.Trim()
            : normalizedEmail;

        ApplicationUser? existing = await _users.GetByEmailAsync(normalizedEmail, cancellationToken).ConfigureAwait(false)
            ?? await _users.GetByUsernameAsync(loginName, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            await _users.AddUserToRoleAsync(existing.Id, RoleNames.Parent, cancellationToken).ConfigureAwait(false);
            await _users.AddUserToSchoolAsync(existing.Id, schoolId, RoleNames.Parent, cancellationToken).ConfigureAwait(false);
            return existing.Id;
        }

        var user = new ApplicationUser
        {
            Username = loginName,
            Email = normalizedEmail,
            IsActive = true,
            LockoutEnabled = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, DefaultPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        await _users.CreateAsync(user, cancellationToken).ConfigureAwait(false);
        await _users.AddUserToRoleAsync(user.Id, RoleNames.Parent, cancellationToken).ConfigureAwait(false);
        await _users.AddUserToSchoolAsync(user.Id, schoolId, RoleNames.Parent, cancellationToken).ConfigureAwait(false);

        return user.Id;
    }
}
