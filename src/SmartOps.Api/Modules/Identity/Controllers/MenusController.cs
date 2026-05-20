using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Identity;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/menus")]
[Authorize]
public sealed class MenusController(IMenuRepository menuRepository) : ControllerBase
{
    [HttpGet("all")]
    [Authorize(Policy = MenuPolicies.Roles.View)]
    [ProducesResponseType(typeof(IReadOnlyList<RoleMenuPermissionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleMenuPermissionDto>>> GetAllMenus(CancellationToken cancellationToken)
    {
        IReadOnlyList<RoleMenuPermissionDto> menus = await menuRepository
            .GetAllMenuTemplatesAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(menus);
    }

    [HttpGet("my")]
    [ProducesResponseType(typeof(IReadOnlyList<MenuDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MenuDto>>> GetMyMenus(
        [FromQuery] string app,
        CancellationToken cancellationToken)
    {
        if (!MenuApplications.IsValid(app))
        {
            return BadRequest("Query parameter 'app' must be CONFIG or SCHOOL.");
        }

        Guid? userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        IReadOnlyList<MenuDto> menus = await menuRepository
            .GetUserMenuTreeAsync(userId.Value, app, cancellationToken)
            .ConfigureAwait(false);

        return Ok(menus);
    }

    private Guid? GetCurrentUserId()
    {
        string? sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out Guid userId) ? userId : null;
    }
}
