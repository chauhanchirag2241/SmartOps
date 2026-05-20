using Microsoft.AspNetCore.Identity;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Application.Modules.Identity.Models;
using SmartOps.Application.Modules.Identity.Utilities;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Domain.Modules.Teacher.Entities;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Infrastructure.Modules.Identity.Services;

public sealed class UserProvisioningService : IUserProvisioningService
{
    private const string ParentFallbackPassword = "ChangeMe@123";
    private const string InternalEmailDomain = "portal.smartops.internal";

    private readonly IUserRepository _users;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;
    private readonly IPersonaRoleMapper _roleMapper;

    public UserProvisioningService(
        IUserRepository users,
        IPasswordHasher<ApplicationUser> passwordHasher,
        IPersonaRoleMapper roleMapper)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _roleMapper = roleMapper;
    }

    public async Task<ProvisionUserResult?> ProvisionSchoolUserAsync(
        ProvisionUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.PortalAccess)
        {
            return null;
        }

        if (!TryResolveLoginIdentity(
                request.SchoolId,
                request.Email,
                request.Username,
                request.LoginIdentifier,
                out string username,
                out string email))
        {
            return null;
        }

        if (!request.DateOfBirth.HasValue)
        {
            throw new InvalidOperationException(
                $"Date of birth is required to provision a {request.RoleName} portal account.");
        }

        // Login uses email; default password must match the email local-part (e.g. vivek2@gmail.com → Vivek2@ddMMyyyy).
        string passwordSeed = ResolvePasswordSeed(request.Email, username);
        string password = DefaultPasswordGenerator.Generate(passwordSeed, request.DateOfBirth.Value);

        ApplicationUser? existing = await _users
            .GetByEmailAsync(email, cancellationToken)
            .ConfigureAwait(false)
            ?? await _users.GetByUsernameAsync(username, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            existing.PasswordHash = _passwordHasher.HashPassword(existing, password);
            existing.SecurityStamp = Guid.NewGuid().ToString("N");
            await _users.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

            await AssignRoleAndSchoolAsync(existing.Id, request.SchoolId, request.RoleName, cancellationToken)
                .ConfigureAwait(false);

            return new ProvisionUserResult
            {
                UserId = existing.Id,
                IsNewUser = false,
                GeneratedPassword = password
            };
        }

        var user = new ApplicationUser
        {
            Username = username,
            Email = email,
            IsActive = true,
            LockoutEnabled = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        await _users.CreateAsync(user, cancellationToken).ConfigureAwait(false);
        await AssignRoleAndSchoolAsync(user.Id, request.SchoolId, request.RoleName, cancellationToken)
            .ConfigureAwait(false);

        return new ProvisionUserResult
        {
            UserId = user.Id,
            IsNewUser = true,
            GeneratedPassword = password
        };
    }

    public async Task<Guid?> ProvisionTeacherUserAsync(
        TeacherEntity teacher,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        string roleName = _roleMapper.ResolveRoleName(teacher.Role, RoleNames.Teacher);

        ProvisionUserResult? result = await ProvisionSchoolUserAsync(
            new ProvisionUserRequest
            {
                SchoolId = schoolId,
                RoleName = roleName,
                PortalAccess = teacher.PortalAccess,
                Email = teacher.Email,
                Username = teacher.Username,
                DateOfBirth = teacher.Dob,
                LoginIdentifier = teacher.EmployeeId
            },
            cancellationToken).ConfigureAwait(false);

        return result?.UserId;
    }

    public async Task<Guid?> ProvisionStudentUserAsync(
        StudentEntity student,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        ProvisionUserResult? result = await ProvisionSchoolUserAsync(
            new ProvisionUserRequest
            {
                SchoolId = schoolId,
                RoleName = RoleNames.Student,
                PortalAccess = student.PortalAccess,
                Email = student.Email,
                Username = null,
                DateOfBirth = student.Dob,
                LoginIdentifier = student.AdmissionNo
            },
            cancellationToken).ConfigureAwait(false);

        return result?.UserId;
    }

    public async Task<Guid?> ProvisionParentUserAsync(
        string email,
        string? username,
        Guid schoolId,
        CancellationToken cancellationToken = default,
        DateOnly? dateOfBirth = null)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        if (dateOfBirth.HasValue
            && TryResolveLoginIdentity(schoolId, email, username, null, out string loginName, out string normalizedEmail))
        {
            ProvisionUserResult? result = await ProvisionSchoolUserAsync(
                new ProvisionUserRequest
                {
                    SchoolId = schoolId,
                    RoleName = RoleNames.Parent,
                    PortalAccess = true,
                    Email = normalizedEmail,
                    Username = loginName,
                    DateOfBirth = dateOfBirth
                },
                cancellationToken).ConfigureAwait(false);

            return result?.UserId;
        }

        return await ProvisionParentWithoutDobAsync(email, username, schoolId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Guid?> ProvisionStaffUserAsync(
        string email,
        string? username,
        string personaRoleLabel,
        Guid schoolId,
        DateOnly dateOfBirth,
        bool portalAccess = true,
        CancellationToken cancellationToken = default)
    {
        string roleName = _roleMapper.ResolveRoleName(personaRoleLabel, RoleNames.Staff);

        ProvisionUserResult? result = await ProvisionSchoolUserAsync(
            new ProvisionUserRequest
            {
                SchoolId = schoolId,
                RoleName = roleName,
                PortalAccess = portalAccess,
                Email = email,
                Username = username,
                DateOfBirth = dateOfBirth
            },
            cancellationToken).ConfigureAwait(false);

        return result?.UserId;
    }

    private async Task<Guid?> ProvisionParentWithoutDobAsync(
        string email,
        string? username,
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        string normalizedEmail = email.Trim().ToLowerInvariant();
        string loginName = !string.IsNullOrWhiteSpace(username)
            ? username.Trim()
            : normalizedEmail;

        ApplicationUser? existing = await _users
            .GetByEmailAsync(normalizedEmail, cancellationToken)
            .ConfigureAwait(false)
            ?? await _users.GetByUsernameAsync(loginName, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            await AssignRoleAndSchoolAsync(existing.Id, schoolId, RoleNames.Parent, cancellationToken)
                .ConfigureAwait(false);
            return existing.Id;
        }

        var user = new ApplicationUser
        {
            Username = loginName,
            Email = normalizedEmail,
            IsActive = true,
            LockoutEnabled = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, ParentFallbackPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        await _users.CreateAsync(user, cancellationToken).ConfigureAwait(false);
        await AssignRoleAndSchoolAsync(user.Id, schoolId, RoleNames.Parent, cancellationToken).ConfigureAwait(false);

        return user.Id;
    }

    private async Task AssignRoleAndSchoolAsync(
        Guid userId,
        Guid schoolId,
        string roleName,
        CancellationToken cancellationToken)
    {
        await _users.AddUserToRoleAsync(userId, roleName, cancellationToken).ConfigureAwait(false);
        await _users.AddUserToSchoolAsync(userId, schoolId, roleName, cancellationToken).ConfigureAwait(false);
    }

    private static string ResolvePasswordSeed(string? email, string loginUsername)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            string trimmed = email.Trim().ToLowerInvariant();
            int atIndex = trimmed.IndexOf('@');
            return atIndex > 0 ? trimmed[..atIndex] : trimmed;
        }

        return loginUsername;
    }

    private static bool TryResolveLoginIdentity(
        Guid schoolId,
        string? email,
        string? explicitUsername,
        string? loginIdentifier,
        out string username,
        out string normalizedEmail)
    {
        username = string.Empty;
        normalizedEmail = string.Empty;

        if (!string.IsNullOrWhiteSpace(explicitUsername))
        {
            username = explicitUsername.Trim().ToLowerInvariant();
        }
        else if (!string.IsNullOrWhiteSpace(email))
        {
            string trimmedEmail = email.Trim().ToLowerInvariant();
            int atIndex = trimmedEmail.IndexOf('@');
            username = atIndex > 0 ? trimmedEmail[..atIndex] : trimmedEmail;
        }
        else if (!string.IsNullOrWhiteSpace(loginIdentifier))
        {
            username = loginIdentifier.Trim().ToLowerInvariant();
        }
        else
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            normalizedEmail = email.Trim().ToLowerInvariant();
            return true;
        }

        string schoolSuffix = schoolId.ToString("N")[..8];
        username = $"{username}.{schoolSuffix}";
        normalizedEmail = $"{username}@{InternalEmailDomain}";
        return true;
    }
}
