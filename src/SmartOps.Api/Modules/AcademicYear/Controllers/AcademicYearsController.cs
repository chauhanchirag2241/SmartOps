using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.AcademicYear;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.AcademicYear.Entities;
using SmartOps.Domain.Modules.AcademicYear;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.AcademicYear.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class AcademicYearsController(
    IAcademicYearRepository academicYearRepository,
    IAuditLogRepository auditLogRepository) : ControllerBase
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

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Title is required.");
        }

        string? dateError = AcademicYearValidation.ValidateYearDates(request.StartDate, request.EndDate);
        if (dateError is not null)
        {
            return BadRequest(dateError);
        }

        if (await academicYearRepository.TitleExistsAsync(request.Title, null, cancellationToken).ConfigureAwait(false))
        {
            return BadRequest("An academic year with this title already exists.");
        }

        var entity = request.ToEntity();
        Guid id = await academicYearRepository.CreateAcademicYearAsync(entity, cancellationToken).ConfigureAwait(false);

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

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.AcademicYears.View)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await auditLogRepository.GetEntityHistoryAsync(
            DatabaseConfig.TableAcademicYears, id, page, pageSize, cancellationToken);

        return Ok(result);
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
    public async Task<IActionResult> UpdateAcademicYear(
        Guid id,
        [FromBody] UpdateAcademicYearDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Academic year data is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest("Title is required.");
        }

        string? dateError = AcademicYearValidation.ValidateYearDates(request.StartDate, request.EndDate);
        if (dateError is not null)
        {
            return BadRequest(dateError);
        }

        if (await academicYearRepository.TitleExistsAsync(request.Title, id, cancellationToken).ConfigureAwait(false))
        {
            return BadRequest("An academic year with this title already exists.");
        }

        try
        {
            await academicYearRepository.UpdateAcademicYearAsync(
                new AcademicYearEntity
                {
                    Id = id,
                    Title = request.Title.Trim(),
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.AcademicYears.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAcademicYear(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await academicYearRepository.DeleteAcademicYearAsync(id, cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
