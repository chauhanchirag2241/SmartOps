using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Application.Modules.Identity.DTOs;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Shared.Constants;

namespace SmartOps.Api.Modules.Identity;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class UsersController(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    ITenantProvider tenantProvider,
    IPasswordHasher<ApplicationUser> passwordHasher) : ControllerBase
{
    private const string DefaultPassword = "ChangeMe@123";

    [HttpGet]
    [Authorize(Policy = MenuPolicies.Users.View)]
    public async Task<ActionResult<IReadOnlyList<SchoolUserDto>>> GetSchoolUsers(CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        return Ok(await BuildSchoolUserDtosAsync(schoolId, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Users.View)]
    public async Task<ActionResult<SchoolUserDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        ApplicationUser? user = await userRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null || !await BelongsToSchoolAsync(user.Id, schoolId, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        IList<string> roles = await userRepository.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        return Ok(ToDto(user, roles));
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.Users.Add)]
    public async Task<ActionResult<SchoolUserDto>> Create(
        [FromBody] CreateUserDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Username and email are required.");
        }

        string email = request.Email.Trim().ToLowerInvariant();
        string username = request.Username.Trim();

        if (await userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false) is not null
            || await userRepository.GetByUsernameAsync(username, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Conflict("A user with this email or username already exists.");
        }

        var user = new ApplicationUser
        {
            Username = username,
            Email = email,
            IsActive = request.IsActive,
            LockoutEnabled = request.LockoutEnabled,
        };
        string password = string.IsNullOrWhiteSpace(request.Password) ? DefaultPassword : request.Password;
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");

        await userRepository.CreateAsync(user, cancellationToken).ConfigureAwait(false);
        await userRepository.AddUserToSchoolAsync(user.Id, schoolId, "Member", cancellationToken).ConfigureAwait(false);

        try
        {
            await SyncUserRolesAsync(user.Id, request.RoleNames, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        IList<string> roles = await userRepository.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, ToDto(user, roles));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Users.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateUserDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        ApplicationUser? user = await userRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null || !await BelongsToSchoolAsync(user.Id, schoolId, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        user.Username = request.Username.Trim();
        user.Email = request.Email.Trim().ToLowerInvariant();
        user.IsActive = request.IsActive;
        user.LockoutEnabled = request.LockoutEnabled;

        await userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpPut("{id:guid}/roles")]
    [Authorize(Policy = MenuPolicies.Users.Edit)]
    public async Task<IActionResult> UpdateRoles(
        Guid id,
        [FromBody] UpdateUserRolesDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        ApplicationUser? user = await userRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null || !await BelongsToSchoolAsync(user.Id, schoolId, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        try
        {
            await SyncUserRolesAsync(user.Id, request.RoleNames, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        return NoContent();
    }

    [HttpPut("{id:guid}/password")]
    [Authorize(Policy = MenuPolicies.Users.Edit)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] ResetUserPasswordDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Password is required.");
        }

        ApplicationUser? user = await userRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (user is null || !await BelongsToSchoolAsync(user.Id, schoolId, cancellationToken).ConfigureAwait(false))
        {
            return NotFound();
        }

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    private async Task<IReadOnlyList<SchoolUserDto>> BuildSchoolUserDtosAsync(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ApplicationUser> users = await userRepository
            .GetUsersBySchoolAsync(schoolId, cancellationToken)
            .ConfigureAwait(false);

        List<SchoolUserDto> result = new();
        foreach (ApplicationUser user in users)
        {
            IList<string> roles = await userRepository.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
            result.Add(ToDto(user, roles));
        }

        return result;
    }

    private async Task<bool> BelongsToSchoolAsync(Guid userId, Guid schoolId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ApplicationUser> schoolUsers = await userRepository
            .GetUsersBySchoolAsync(schoolId, cancellationToken)
            .ConfigureAwait(false);
        return schoolUsers.Any(u => u.Id == userId);
    }

    private async Task SyncUserRolesAsync(
        Guid userId,
        IReadOnlyList<string> targetRoleNames,
        CancellationToken cancellationToken)
    {
        HashSet<string> desired = targetRoleNames
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string roleName in desired)
        {
            if (await roleRepository.GetByNameAsync(roleName, cancellationToken).ConfigureAwait(false) is null)
            {
                throw new InvalidOperationException($"Role '{roleName}' does not exist.");
            }
        }

        IList<string> current = await userRepository.GetRolesAsync(userId, cancellationToken).ConfigureAwait(false);
        foreach (string role in current.Where(r => !desired.Contains(r)))
        {
            await userRepository.RemoveUserFromRoleAsync(userId, role, cancellationToken).ConfigureAwait(false);
        }

        foreach (string role in desired.Where(r => !current.Contains(r, StringComparer.OrdinalIgnoreCase)))
        {
            await userRepository.AddUserToRoleAsync(userId, role, cancellationToken).ConfigureAwait(false);
        }
    }

    private static SchoolUserDto ToDto(ApplicationUser user, IList<string> roles) =>
        new()
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            IsActive = user.IsActive,
            LockoutEnabled = user.LockoutEnabled,
            Roles = roles.ToList(),
        };

    private bool TryGetSchoolId(out Guid schoolId)
    {
        schoolId = Guid.Empty;
        string? raw = tenantProvider.GetCurrentSchoolId();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out schoolId);
    }
}
