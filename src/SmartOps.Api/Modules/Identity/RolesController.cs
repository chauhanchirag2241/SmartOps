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
    [Authorize(Policy = PermissionNames.HrRead)]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyList<ApplicationRole> roles = await roleRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        List<RoleDto> result = new();

        foreach (ApplicationRole role in roles)
        {
            IReadOnlyList<string> permissions = await roleRepository
                .GetPermissionNamesForRoleAsync(role.Id, cancellationToken)
                .ConfigureAwait(false);

            result.Add(new RoleDto
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                Permissions = permissions
            });
        }

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionNames.RolesManage)]
    public async Task<ActionResult<RoleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        ApplicationRole? role = await roleRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return NotFound();
        }

        IReadOnlyList<string> permissions = await roleRepository
            .GetPermissionNamesForRoleAsync(role.Id, cancellationToken)
            .ConfigureAwait(false);

        return Ok(new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            Permissions = permissions
        });
    }

    [HttpGet("{id:guid}/users")]
    [Authorize(Policy = PermissionNames.RolesManage)]
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
    [Authorize(Policy = PermissionNames.RolesManage)]
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
    [Authorize(Policy = PermissionNames.RolesManage)]
    public async Task<IActionResult> UpdatePermissions(
        Guid id,
        [FromBody] UpdateRolePermissionsDto request,
        CancellationToken cancellationToken)
    {
        ApplicationRole? role = await roleRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return NotFound();
        }

        await roleRepository
            .SetRolePermissionsAsync(id, request.PermissionNames, cancellationToken)
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
