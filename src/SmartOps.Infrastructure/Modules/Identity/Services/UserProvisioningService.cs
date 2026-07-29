using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Application.Modules.Identity.Models;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.Employee.Entities;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Domain.Modules.Student.Entities;

namespace SmartOps.Infrastructure.Modules.Identity.Services;

public sealed class UserProvisioningService : IUserProvisioningService
{
    public const string DefaultPortalPassword = "SmartOps@123";

    private readonly IUserRepository _users;
    private readonly IUserTypeRepository _userTypes;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

    public UserProvisioningService(
        IUserRepository users,
        IUserTypeRepository userTypes,
        IPasswordHasher<ApplicationUser> passwordHasher)
    {
        _users = users;
        _userTypes = userTypes;
        _passwordHasher = passwordHasher;
    }

    public async Task<ProvisionUserResult> ProvisionSchoolUserAsync(
        ProvisionUserRequest request,
        CancellationToken cancellationToken = default)
    {
        string firstName = RequireName(request.FirstName, "First name");
        string lastName = RequireName(request.LastName, "Last name");
        string email = RequireEmail(request.Email);
        string username = BuildUsername(firstName, lastName, request.Username);
        string userTypeCode = string.IsNullOrWhiteSpace(request.UserTypeCode)
            ? UserTypeCodes.Teacher
            : request.UserTypeCode.Trim();

        Guid? userTypeId = await _userTypes.GetIdByCodeAsync(userTypeCode, cancellationToken).ConfigureAwait(false)
            ?? UserTypeCodes.TryGetId(userTypeCode);

        if (userTypeId is null || userTypeId == Guid.Empty)
        {
            throw new InvalidOperationException($"Unknown user type '{userTypeCode}'.");
        }

        ApplicationUser? byUsername = await _users.GetByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        if (byUsername is not null)
        {
            throw new InvalidOperationException($"Username '{username}' already exists.");
        }

        ApplicationUser? byEmail = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (byEmail is not null)
        {
            throw new InvalidOperationException($"Email '{email}' already exists.");
        }

        var user = new ApplicationUser
        {
            FirstName = firstName,
            LastName = lastName,
            Mobile = string.IsNullOrWhiteSpace(request.Mobile) ? null : request.Mobile.Trim(),
            UserTypeId = userTypeId.Value,
            Username = username,
            Email = email,
            IsActive = true,
            LockoutEnabled = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, DefaultPortalPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        await _users.CreateAsync(user, cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(request.RoleName))
        {
            await _users.AddUserToRoleAsync(user.Id, request.RoleName.Trim(), cancellationToken).ConfigureAwait(false);
        }

        return new ProvisionUserResult
        {
            UserId = user.Id,
            IsNewUser = true,
            GeneratedPassword = DefaultPortalPassword
        };
    }

    public async Task<Guid> ProvisionEmployeeUserAsync(
        EmployeeEntity employee,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        string userTypeCode = string.IsNullOrWhiteSpace(employee.UserTypeCode)
            ? UserTypeCodes.Teacher
            : employee.UserTypeCode;

        if (!UserTypeCodes.IsStaff(userTypeCode))
        {
            userTypeCode = UserTypeCodes.Teacher;
        }

        ProvisionUserResult result = await ProvisionSchoolUserAsync(
            new ProvisionUserRequest
            {
                SchoolId = schoolId,
                UserTypeCode = userTypeCode,
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Email = employee.Email,
                Mobile = employee.Mobile,
                Username = employee.Username
            },
            cancellationToken).ConfigureAwait(false);

        employee.UserId = result.UserId;
        employee.Username = BuildUsername(employee.FirstName, employee.LastName, employee.Username);
        return result.UserId;
    }

    public async Task<Guid> ProvisionStudentUserAsync(
        StudentEntity student,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(student.Email))
        {
            throw new InvalidOperationException("Student email is required to create a portal user.");
        }

        ProvisionUserResult result = await ProvisionSchoolUserAsync(
            new ProvisionUserRequest
            {
                SchoolId = schoolId,
                UserTypeCode = UserTypeCodes.Student,
                FirstName = student.FirstName,
                LastName = student.LastName,
                Email = student.Email,
                Mobile = student.Mobile
            },
            cancellationToken).ConfigureAwait(false);

        student.UserId = result.UserId;
        return result.UserId;
    }

    public async Task<Guid?> ProvisionStaffUserAsync(
        string email,
        string? username,
        string firstName,
        string lastName,
        string? mobile,
        string personaRoleLabel,
        string userTypeCode,
        Guid schoolId,
        CancellationToken cancellationToken = default)
    {
        _ = personaRoleLabel;
        ProvisionUserResult result = await ProvisionSchoolUserAsync(
            new ProvisionUserRequest
            {
                SchoolId = schoolId,
                UserTypeCode = string.IsNullOrWhiteSpace(userTypeCode) ? UserTypeCodes.OfficeStaff : userTypeCode,
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Mobile = mobile,
                Username = username
            },
            cancellationToken).ConfigureAwait(false);

        return result.UserId;
    }

    public static string BuildUsername(string firstName, string lastName, string? explicitUsername = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitUsername))
        {
            return SanitizeUsername(explicitUsername);
        }

        string first = SanitizeUsername(firstName);
        string last = SanitizeUsername(lastName);
        string username = string.IsNullOrEmpty(last) ? first : $"{first}.{last}";
        if (string.IsNullOrEmpty(username))
        {
            throw new InvalidOperationException("Unable to generate username from first and last name.");
        }

        return username;
    }

    private static string SanitizeUsername(string value) =>
        Regex.Replace(value.Trim().ToLowerInvariant(), @"[^a-z0-9.]", string.Empty);

    private static string RequireName(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value.Trim();
    }

    private static string RequireEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Email is required.");
        }

        return email.Trim();
    }
}
