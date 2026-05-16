using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Identity.DTOs;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Modules.Identity.Entities;
using SmartOps.Application.Common.Abstractions;
using SmartOps.Shared.Constants;

namespace SmartOps.Api.Modules.Identity;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class UsersController(IUserRepository userRepository, ITenantProvider tenantProvider) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = PermissionNames.HrRead)]
    public async Task<ActionResult<IReadOnlyList<SchoolUserDto>>> GetSchoolUsers(CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        IReadOnlyList<ApplicationUser> users = await userRepository
            .GetUsersBySchoolAsync(schoolId, cancellationToken)
            .ConfigureAwait(false);

        List<SchoolUserDto> result = new();
        foreach (ApplicationUser user in users)
        {
            IList<string> roles = await userRepository.GetRolesAsync(user.Id, cancellationToken).ConfigureAwait(false);
            result.Add(new SchoolUserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Roles = roles.ToList()
            });
        }

        return Ok(result);
    }

    private bool TryGetSchoolId(out Guid schoolId)
    {
        schoolId = Guid.Empty;
        string? raw = tenantProvider.GetCurrentSchoolId();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out schoolId);
    }
}
