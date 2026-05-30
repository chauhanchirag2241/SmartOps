using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.AcademicYear;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.AcademicYear.Entities;
using SmartOps.Domain.Modules.AcademicYear;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.AcademicYear.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AcademicYearsController(IAcademicYearRepository academicYearRepository) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = MenuPolicies.AcademicYears.Add)]
    [ProducesResponseType(typeof(CreateAcademicYearResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateAcademicYearResponse>> CreateAcademicYear(
        [FromBody] CreateAcademicYearDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Academic year data is required.");
        }

        var entity = request.ToEntity();
        var id = await academicYearRepository.CreateAcademicYearAsync(entity, cancellationToken).ConfigureAwait(false);

        return Ok(new CreateAcademicYearResponse("Academic year created successfully", id));
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.AcademicYears.View)]
    [ProducesResponseType(typeof(PagedResult<AcademicYearListModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllAcademicYears(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] AcademicYearFilter filter = AcademicYearFilter.Active,
        CancellationToken cancellationToken = default)
    {
        var result = await academicYearRepository
            .GetAllAcademicYearsAsync(pageIndex, pageSize, searchTerm, sortColumn, sortDirection, filter, cancellationToken)
            .ConfigureAwait(false);

        return Ok(result);
    }

    [HttpGet("/api/academic-year/current")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentAcademicYearDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentAcademicYearDto>> GetCurrentAcademicYear(CancellationToken cancellationToken)
    {
        var entity = await academicYearRepository.GetCurrentAcademicYearAsync(cancellationToken).ConfigureAwait(false);
        return entity is null ? NotFound() : Ok(entity.ToCurrentDto());
    }

    [HttpGet("/api/academic-year/dropdown")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<AcademicYearDropdownDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAcademicYearDropdown(
        [FromQuery] string? scope,
        CancellationToken cancellationToken)
    {
        bool currentAndFutureOnly = string.Equals(scope, "switcher", StringComparison.OrdinalIgnoreCase);
        var result = await academicYearRepository
            .GetAcademicYearDropdownAsync(currentAndFutureOnly, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result.Select(i => i.ToDropdownDto()).ToList());
    }

    [HttpPut("{id:guid}/set-current")]
    [Authorize(Policy = MenuPolicies.AcademicYears.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetCurrentAcademicYear(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await academicYearRepository.SetCurrentAcademicYearAsync(id, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.AcademicYears.View)]
    [ProducesResponseType(typeof(AcademicYearEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AcademicYearEntity>> GetAcademicYearById(Guid id, CancellationToken cancellationToken)
    {
        var entity = await academicYearRepository.GetAcademicYearByIdAsync(id, cancellationToken, includeInactive: true).ConfigureAwait(false);
        return entity is null ? NotFound() : Ok(entity);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.AcademicYears.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateAcademicYear(Guid id, [FromBody] AcademicYearEntity entity, CancellationToken cancellationToken)
    {
        if (id != entity.Id)
        {
            return BadRequest("Route id and payload id must match.");
        }

        await academicYearRepository.UpdateAcademicYearAsync(entity, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.AcademicYears.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAcademicYear(Guid id, CancellationToken cancellationToken)
    {
        await academicYearRepository.DeleteAcademicYearAsync(id, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("{id:guid}/semesters")]
    [Authorize(Policy = MenuPolicies.AcademicYears.View)]
    [ProducesResponseType(typeof(IList<AcademicYearSemesterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSemesters(Guid id, CancellationToken cancellationToken)
    {
        var semesters = await academicYearRepository.GetSemestersAsync(id, cancellationToken).ConfigureAwait(false);
        return Ok(semesters.Select(s => s.ToDto()).ToList());
    }

    [HttpPut("{id:guid}/semesters")]
    [Authorize(Policy = MenuPolicies.AcademicYears.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveSemesters(
        Guid id,
        [FromBody] SaveAcademicYearSemestersRequest request,
        CancellationToken cancellationToken)
    {
        if (request?.Semesters is null || request.Semesters.Count == 0)
        {
            return BadRequest("At least one semester is required.");
        }

        await academicYearRepository.SaveSemestersAsync(
            id,
            request.Semesters.Select(s => s.ToInput()).ToList(),
            cancellationToken).ConfigureAwait(false);
        return NoContent();
    }
}
