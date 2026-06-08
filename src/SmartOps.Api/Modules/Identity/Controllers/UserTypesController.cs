using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.Identity.Entities;

namespace SmartOps.Api.Modules.Identity.Controllers;

[ApiController]
[Route("api/user-types")]
[Authorize]
public sealed class UserTypesController(IUserTypeRepository userTypeRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = MenuPolicies.Users.View)]
    public async Task<ActionResult<IReadOnlyList<UserTypeListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyList<UserTypeEntity> types = await userTypeRepository
            .GetAllActiveAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<UserTypeListItemDto> result = types
            .Select(t => new UserTypeListItemDto(t.Id, t.Code, t.Name))
            .ToList();

        return Ok(result);
    }
}

public sealed record UserTypeListItemDto(Guid Id, string Code, string Name);
