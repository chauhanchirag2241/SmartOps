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
public sealed class PermissionsController(IPermissionRepository permissionRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionNames.RolesManage)]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyList<Permission> permissions = await permissionRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return Ok(permissions.Select(p => new PermissionDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description
        }).ToList());
    }
}
