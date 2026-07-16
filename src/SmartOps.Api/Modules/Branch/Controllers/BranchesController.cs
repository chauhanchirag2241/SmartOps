using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Abstractions;
using SmartOps.Application.Modules.Branch;
using SmartOps.Application.Modules.Branch.Interfaces;
using SmartOps.Application.Modules.Identity.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Branch.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class BranchesController(
    IBranchRepository branchRepository,
    IUserRepository userRepository,
    ITenantProvider tenantProvider) : ControllerBase
{
    [HttpGet("my")]
    public async Task<ActionResult<MyBranchesResponseDto>> GetMyBranches(CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId) || !TryGetUserId(out Guid userId))
        {
            return BadRequest("School context is required.");
        }

        IList<string> roleCodes = await userRepository
            .GetRoleCodesAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        bool canViewAll = roleCodes.Any(c => RoleCodes.GlobalScopeRoles.Contains(c));
        IReadOnlyList<BranchDropdownItemDto> branches = canViewAll
            ? await branchRepository.GetBranchesBySchoolAsync(schoolId, cancellationToken).ConfigureAwait(false)
            : await branchRepository.GetUserBranchesAsync(userId, schoolId, cancellationToken).ConfigureAwait(false);

        return Ok(new MyBranchesResponseDto
        {
            Branches = branches,
            CanViewAllBranches = canViewAll
        });
    }

    [HttpGet("school/{schoolId:guid}")]
    [Authorize(Policy = MenuPolicies.Users.View)]
    public async Task<ActionResult<IReadOnlyList<BranchDropdownItemDto>>> GetSchoolBranches(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        return Ok(await branchRepository.GetBranchesBySchoolAsync(schoolId, cancellationToken).ConfigureAwait(false));
    }

    [HttpPut("users/{userId:guid}")]
    [Authorize(Policy = MenuPolicies.Users.Edit)]
    public async Task<IActionResult> SetUserBranches(
        Guid userId,
        [FromBody] UpdateUserBranchesDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        await branchRepository
            .SetUserBranchesAsync(userId, schoolId, request.BranchIds, request.DefaultBranchId, cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }

    [HttpGet("users/{userId:guid}")]
    [Authorize(Policy = MenuPolicies.Users.View)]
    public async Task<ActionResult<IReadOnlyList<BranchDropdownItemDto>>> GetUserBranches(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!TryGetSchoolId(out Guid schoolId))
        {
            return BadRequest("School context is required.");
        }

        return Ok(await branchRepository.GetUserBranchesAsync(userId, schoolId, cancellationToken).ConfigureAwait(false));
    }

    private bool TryGetSchoolId(out Guid schoolId)
    {
        schoolId = Guid.Empty;
        string? raw = tenantProvider.GetCurrentSchoolId();
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out schoolId);
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        string? sub = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return !string.IsNullOrWhiteSpace(sub) && Guid.TryParse(sub, out userId);
    }
}
