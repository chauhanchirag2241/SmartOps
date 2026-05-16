using Microsoft.AspNetCore.Authorization;
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
public sealed class RolesController(
    IRoleRepository roleRepository,
    IUserRepository userRepository,
    ITenantProvider tenantProvider) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = MenuPolicies.Roles.View)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyList<ApplicationRole> roles = await roleRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        List<RoleDto> result = new();

        foreach (ApplicationRole role in roles)
        {
            IReadOnlyList<RoleMenuPermissionDto> permissions = await roleRepository
                .GetMenuPermissionsForRoleAsync(role.Id, cancellationToken)
                .ConfigureAwait(false);

            result.Add(new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Code = role.Code,
                Description = role.Description,
                MenuPermissions = permissions
            });
        }

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Roles.View)]
    public async Task<ActionResult<RoleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        ApplicationRole? role = await roleRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return NotFound();
        }

        IReadOnlyList<RoleMenuPermissionDto> permissions = await roleRepository
            .GetMenuPermissionsForRoleAsync(role.Id, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Code = role.Code,
            Description = role.Description,
            MenuPermissions = permissions
        });
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.Roles.Add)]
    public async Task<ActionResult<RoleDto>> Create(
        [FromBody] CreateRoleDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest("Role name and code are required.");
        }

        string name = request.Name.Trim();
        string code = request.Code.Trim().ToUpperInvariant();
        if (await roleRepository.GetByNameAsync(name, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Conflict("A role with this name already exists.");
        }

        var role = new ApplicationRole
        {
            Name = name,
            Code = code,
            Description = request.Description?.Trim(),
            IsActive = true,
        };

        await roleRepository.CreateAsync(role, cancellationToken).ConfigureAwait(false);
        await roleRepository
            .SetRoleMenuPermissionsAsync(role.Id, request.MenuPermissions, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<RoleMenuPermissionDto> permissions = await roleRepository
            .GetMenuPermissionsForRoleAsync(role.Id, cancellationToken)
            .ConfigureAwait(false);

        return CreatedAtAction(
            nameof(GetById),
            new { id = role.Id },
            new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Code = role.Code,
                Description = role.Description,
                MenuPermissions = permissions,
            });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Roles.Edit)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateRoleDto request,
        CancellationToken cancellationToken)
    {
        ApplicationRole? role = await roleRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return NotFound();
        }

        string name = request.Name.Trim();
        string code = request.Code.Trim().ToUpperInvariant();

        IReadOnlyList<ApplicationRole> allRoles = await roleRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        if (allRoles.Any(r => r.Id != id && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict("A role with this name already exists.");
        }

        if (allRoles.Any(r => r.Id != id && string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict("A role with this code already exists.");
        }

        role.Name = name;
        role.Code = code;
        role.Description = request.Description?.Trim();
        role.IsActive = request.IsActive;

        await roleRepository.UpdateAsync(role, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("{id:guid}/users")]
    [Authorize(Policy = MenuPolicies.Roles.View)]
    public async Task<ActionResult<IReadOnlyList<SchoolUserDto>>> GetUsersInRole(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        ApplicationRole? role = await roleRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return NotFound();
        }

        IReadOnlyList<ApplicationUser> schoolUsers = await userRepository
            .GetUsersBySchoolAsync(schoolId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<ApplicationUser> roleUsers = await userRepository
            .GetUsersInRoleAsync(role.Name, cancellationToken)
            .ConfigureAwait(false);

        HashSet<Guid> roleUserIds = roleUsers.Select(u => u.Id).ToHashSet();
        List<SchoolUserDto> result = new();

        foreach (ApplicationUser user in schoolUsers.Where(u => roleUserIds.Contains(u.Id)))
        {
            IList<string> roles = await userRepository.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
            result.Add(new SchoolUserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                IsActive = user.IsActive,
                Roles = roles.ToList()
            });
        }

        return Ok(result);
    }

    [HttpPut("{id:guid}/users")]
    [Authorize(Policy = MenuPolicies.Roles.Edit)]
    public async Task<IActionResult> AssignUsers(
        Guid id,
        [FromBody] AssignRoleUsersDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        ApplicationRole? role = await roleRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return NotFound();
        }

        IReadOnlyList<ApplicationUser> schoolUsers = await userRepository
            .GetUsersBySchoolAsync(schoolId, cancellationToken)
            .ConfigureAwait(false);
        HashSet<Guid> schoolUserIds = schoolUsers.Select(u => u.Id).ToHashSet();
        HashSet<Guid> desiredIds = request.UserIds.Where(schoolUserIds.Contains).ToHashSet();

        foreach (ApplicationUser user in schoolUsers)
        {
            IList<string> userRoles = await userRepository.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
            bool hasRole = userRoles.Contains(role.Name, StringComparer.OrdinalIgnoreCase);
            bool shouldHave = desiredIds.Contains(user.Id);

            if (shouldHave && !hasRole)
            {
                await userRepository.AddUserToRoleAsync(user.Id, role.Name, cancellationToken).ConfigureAwait(false);
            }
            else if (!shouldHave && hasRole)
            {
                await userRepository.RemoveUserFromRoleAsync(user.Id, role.Name, cancellationToken).ConfigureAwait(false);
            }
        }

        return NoContent();
    }

    [HttpPut("{id:guid}/permissions")]
    [Authorize(Policy = MenuPolicies.Roles.Edit)]
    public async Task<IActionResult> UpdatePermissions(
        Guid id,
        [FromBody] UpdateRoleMenuPermissionsDto request,
        CancellationToken cancellationToken)
    {
        ApplicationRole? role = await roleRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return NotFound();
        }

        await roleRepository
            .SetRoleMenuPermissionsAsync(id, request.Permissions, cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }

    private bool TryGetSchoolId(out Guid schoolId)
    {
        schoolId = Guid.Empty;
        string? raw = tenantProvider.GetCurrentSchoolId();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out schoolId);
    }
}
