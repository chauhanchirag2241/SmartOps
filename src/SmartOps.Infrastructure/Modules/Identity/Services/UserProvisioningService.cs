using System.Data;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Application.Modules.Identity.Models;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.Employee.Entities;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Domain.Modules.Student.Entities;
using SmartOps.Infrastructure.Persistence;
using SmartOps.Infrastructure.Persistence.Context;

namespace SmartOps.Infrastructure.Modules.Identity.Services;

public sealed class UserProvisioningService : BaseRepository, IUserProvisioningService
{
    public const string DefaultPortalPassword = "SmartOps@123";

    private readonly IUserRepository _users;
    private readonly IUserTypeRepository _userTypes;
    private readonly IPasswordHasher<ApplicationUser> _passwordHasher;

    public UserProvisioningService(
        DapperContext context,
        ICurrentUserService currentUser,
        IUserRepository users,
        IUserTypeRepository userTypes,
        IPasswordHasher<ApplicationUser> passwordHasher)
        : base(context, currentUser)
    {
        _users = users;
        _userTypes = userTypes;
        _passwordHasher = passwordHasher;
    }

    public async Task<ProvisionUserResult> ProvisionSchoolUserAsync(
        ProvisionUserRequest request,
        CancellationToken cancellationToken = default)
    {
        IDbConnection connection = await Context.GetGlobalConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await WithTransactionAsync(
                connection,
                (conn, tx) => ProvisionSchoolUserCoreAsync(request, tx, cancellationToken))
            .ConfigureAwait(false);
    }

    public Task<ProvisionUserResult> ProvisionSchoolUserAsync(
        ProvisionUserRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        ProvisionSchoolUserCoreAsync(request, transaction, cancellationToken);

    private async Task<ProvisionUserResult> ProvisionSchoolUserCoreAsync(
        ProvisionUserRequest request,
        IDbTransaction transaction,
        CancellationToken cancellationToken)
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

        ApplicationUser? byUsername = await _users
            .GetByUsernameAsync(username, transaction, cancellationToken)
            .ConfigureAwait(false);
        if (byUsername is not null)
        {
            throw new InvalidOperationException($"Username '{username}' already exists.");
        }

        ApplicationUser? byEmail = await _users
            .GetByEmailAsync(email, transaction, cancellationToken)
            .ConfigureAwait(false);
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
            LockoutEnabled = true,
            MustChangePassword = true
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, DefaultPortalPassword);
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        await _users.CreateAsync(user, transaction, cancellationToken).ConfigureAwait(false);

        string? roleName = string.IsNullOrWhiteSpace(request.RoleName)
            ? RoleNames.FromUserType(userTypeCode)
            : request.RoleName.Trim();
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            await _users.AddUserToRoleAsync(user.Id, roleName, transaction, cancellationToken).ConfigureAwait(false);
        }

        return new ProvisionUserResult
        {
            UserId = user.Id,
            IsNewUser = true,
            GeneratedPassword = DefaultPortalPassword
        };
    }

    public Task<Guid> ProvisionEmployeeUserAsync(
        EmployeeEntity employee,
        Guid schoolId,
        CancellationToken cancellationToken = default) =>
        ProvisionEmployeeUserCoreAsync(employee, schoolId, transaction: null, cancellationToken);

    public Task<Guid> ProvisionEmployeeUserAsync(
        EmployeeEntity employee,
        Guid schoolId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        ProvisionEmployeeUserCoreAsync(employee, schoolId, transaction, cancellationToken);

    private async Task<Guid> ProvisionEmployeeUserCoreAsync(
        EmployeeEntity employee,
        Guid schoolId,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        string userTypeCode = string.IsNullOrWhiteSpace(employee.UserTypeCode)
            ? UserTypeCodes.Teacher
            : employee.UserTypeCode;

        if (!UserTypeCodes.IsStaff(userTypeCode))
        {
            userTypeCode = UserTypeCodes.Teacher;
        }

        var request = new ProvisionUserRequest
        {
            SchoolId = schoolId,
            UserTypeCode = userTypeCode,
            RoleName = RoleNames.ResolveForProvision(userTypeCode, employee.PortalRoleName),
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            Email = employee.Email,
            Mobile = employee.Mobile,
            Username = employee.Username
        };

        ProvisionUserResult result = transaction is null
            ? await ProvisionSchoolUserAsync(request, cancellationToken).ConfigureAwait(false)
            : await ProvisionSchoolUserAsync(request, transaction, cancellationToken).ConfigureAwait(false);

        employee.UserId = result.UserId;
        employee.Username = BuildUsername(employee.FirstName, employee.LastName, employee.Username);
        return result.UserId;
    }

    public Task<Guid> ProvisionStudentUserAsync(
        StudentEntity student,
        Guid schoolId,
        CancellationToken cancellationToken = default) =>
        ProvisionStudentUserCoreAsync(student, schoolId, transaction: null, cancellationToken);

    public Task<Guid> ProvisionStudentUserAsync(
        StudentEntity student,
        Guid schoolId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        ProvisionStudentUserCoreAsync(student, schoolId, transaction, cancellationToken);

    private async Task<Guid> ProvisionStudentUserCoreAsync(
        StudentEntity student,
        Guid schoolId,
        IDbTransaction? transaction,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(student.Email))
        {
            throw new InvalidOperationException("Student email is required to create a portal user.");
        }

        // Identity user only — no SmartOpsUI portal role (mobile app role later).
        var request = new ProvisionUserRequest
        {
            SchoolId = schoolId,
            UserTypeCode = UserTypeCodes.Student,
            RoleName = null,
            FirstName = student.FirstName,
            LastName = student.LastName,
            Email = student.Email,
            Mobile = student.Mobile
        };

        ProvisionUserResult result = transaction is null
            ? await ProvisionSchoolUserAsync(request, cancellationToken).ConfigureAwait(false)
            : await ProvisionSchoolUserAsync(request, transaction, cancellationToken).ConfigureAwait(false);

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
        string resolvedType = string.IsNullOrWhiteSpace(userTypeCode) ? UserTypeCodes.OfficeStaff : userTypeCode;
        ProvisionUserResult result = await ProvisionSchoolUserAsync(
            new ProvisionUserRequest
            {
                SchoolId = schoolId,
                UserTypeCode = resolvedType,
                RoleName = RoleNames.ResolveForProvision(resolvedType, personaRoleLabel),
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
