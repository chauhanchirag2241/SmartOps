using Microsoft.AspNetCore.Identity;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Teacher.Entities;

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
            await _users.AddUserToRoleAsync(existing.Id, "Teacher", cancellationToken).ConfigureAwait(false);
            await _users.AddUserToSchoolAsync(existing.Id, schoolId, "Teacher", cancellationToken).ConfigureAwait(false);
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
        await _users.AddUserToRoleAsync(user.Id, "Teacher", cancellationToken).ConfigureAwait(false);
        await _users.AddUserToSchoolAsync(user.Id, schoolId, "Teacher", cancellationToken).ConfigureAwait(false);

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
            await _users.AddUserToRoleAsync(existing.Id, "Student", cancellationToken).ConfigureAwait(false);
            await _users.AddUserToSchoolAsync(existing.Id, schoolId, "Student", cancellationToken).ConfigureAwait(false);
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
        await _users.AddUserToRoleAsync(user.Id, "Student", cancellationToken).ConfigureAwait(false);
        await _users.AddUserToSchoolAsync(user.Id, schoolId, "Student", cancellationToken).ConfigureAwait(false);

        return user.Id;
    }
}
