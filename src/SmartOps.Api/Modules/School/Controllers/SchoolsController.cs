using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.School;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.School.Entities;
using SmartOps.Domain.Modules.School;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.School.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SchoolsController(ISchoolRepository schoolRepository) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = MenuPolicies.Schools.Add)]
    [ProducesResponseType(typeof(CreateSchoolResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateSchoolResponse>> CreateSchool(
        [FromBody] CreateSchoolDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("School data is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Subdomain)
            || string.IsNullOrWhiteSpace(request.SchoolCode))
        {
            return BadRequest("Name, subdomain and school code are required.");
        }

        var entity = request.ToEntity();
        var schoolId = await schoolRepository.CreateSchoolAsync(entity, cancellationToken).ConfigureAwait(false);
        return Ok(new CreateSchoolResponse("School created successfully", schoolId));
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.Schools.View)]
    [ProducesResponseType(typeof(PagedResult<SchoolListModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllSchools(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] SchoolFilter filter = SchoolFilter.Active,
        CancellationToken cancellationToken = default)
    {
        var result = await schoolRepository
            .GetAllSchoolsAsync(pageIndex, pageSize, searchTerm, sortColumn, sortDirection, filter, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("by-subdomain/{subdomain}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SchoolBootstrapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchoolBootstrapDto>> GetSchoolBySubdomain(string subdomain, CancellationToken cancellationToken)
    {
        var school = await schoolRepository.GetSchoolBySubdomainAsync(subdomain, cancellationToken).ConfigureAwait(false);
        return school is null ? NotFound() : Ok(school.ToBootstrapDto());
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Schools.View)]
    [ProducesResponseType(typeof(SchoolEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchoolEntity>> GetSchoolById(Guid id, CancellationToken cancellationToken)
    {
        var school = await schoolRepository.GetSchoolByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return school is null ? NotFound() : Ok(school);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Schools.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSchool(Guid id, [FromBody] UpdateSchoolDto request, CancellationToken cancellationToken)
    {
        if (request is null || id != request.Id)
        {
            return BadRequest("Route id and payload id must match.");
        }

        var school = await schoolRepository.GetSchoolByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (school is null)
        {
            return NotFound();
        }

        request.ApplyTo(school);
        await schoolRepository.UpdateSchoolAsync(school, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Schools.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSchool(Guid id, CancellationToken cancellationToken)
    {
        await schoolRepository.DeleteSchoolAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("{schoolId:guid}/branches")]
    [Authorize(Policy = MenuPolicies.Schools.View)]
    [ProducesResponseType(typeof(IReadOnlyList<SchoolBranchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SchoolBranchDto>>> GetBranches(
        Guid schoolId,
        CancellationToken cancellationToken)
    {
        var branches = await schoolRepository.GetBranchesAsync(schoolId, cancellationToken).ConfigureAwait(false);
        return Ok(branches.Select(b => b.ToDto()).ToList());
    }

    [HttpPost("{schoolId:guid}/branches")]
    [Authorize(Policy = MenuPolicies.Schools.Edit)]
    [ProducesResponseType(typeof(SchoolBranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SchoolBranchDto>> AddBranch(
        Guid schoolId,
        [FromBody] SaveSchoolBranchDto request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Branch name is required.");
        }

        try
        {
            var branch = await schoolRepository
                .AddBranchAsync(schoolId, request.Name, request.Email, request.Address, cancellationToken)
                .ConfigureAwait(false);
            return Ok(branch.ToDto());
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{schoolId:guid}/branches/{branchId:guid}")]
    [Authorize(Policy = MenuPolicies.Schools.Edit)]
    [ProducesResponseType(typeof(SchoolBranchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchoolBranchDto>> UpdateBranch(
        Guid schoolId,
        Guid branchId,
        [FromBody] SaveSchoolBranchDto request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Branch name is required.");
        }

        var branch = await schoolRepository
            .UpdateBranchAsync(schoolId, branchId, request.Name, request.Email, request.Address, cancellationToken)
            .ConfigureAwait(false);
        return branch is null ? NotFound() : Ok(branch.ToDto());
    }

    [HttpDelete("{schoolId:guid}/branches/{branchId:guid}")]
    [Authorize(Policy = MenuPolicies.Schools.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBranch(
        Guid schoolId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        try
        {
            var ok = await schoolRepository.DeactivateBranchAsync(schoolId, branchId, cancellationToken)
                .ConfigureAwait(false);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
