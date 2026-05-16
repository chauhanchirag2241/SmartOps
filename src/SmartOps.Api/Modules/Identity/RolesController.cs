using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Identity.DTOs;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Shared.Constants;

namespace SmartOps.Api.Modules.Identity;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class RolesController(IRoleRepository roleRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionNames.RolesManage)]
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
}
