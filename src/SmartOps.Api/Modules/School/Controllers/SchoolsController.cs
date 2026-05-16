using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.School.DTOs;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.School.Entities;
using SmartOps.Domain.Modules.School.Interfaces;
using SmartOps.Domain.Modules.School.Models;

namespace SmartOps.Api.Modules.School.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class SchoolsController(ISchoolRepository schoolRepository) : ControllerBase
{
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CreateSchoolResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateSchoolResponse>> CreateSchool(
        [FromBody] CreateSchoolDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("School data is required.");
        }

        var entity = request.ToEntity();
        var schoolId = await schoolRepository.CreateSchoolAsync(entity, cancellationToken).ConfigureAwait(false);
        return Ok(new CreateSchoolResponse("School created successfully", schoolId));
    }

    [HttpGet]
    [AllowAnonymous]
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

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SchoolEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SchoolEntity>> GetSchoolById(Guid id, CancellationToken cancellationToken)
    {
        var school = await schoolRepository.GetSchoolByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return school is null ? NotFound() : Ok(school);
    }

    [HttpPut("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSchool(Guid id, [FromBody] SchoolEntity school, CancellationToken cancellationToken)
    {
        if (id != school.Id)
        {
            return BadRequest("Route id and payload id must match.");
        }

        await schoolRepository.UpdateSchoolAsync(school, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteSchool(Guid id, CancellationToken cancellationToken)
    {
        await schoolRepository.DeleteSchoolAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
