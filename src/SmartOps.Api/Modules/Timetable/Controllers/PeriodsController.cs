using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Application.Modules.Timetable;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Timetable;
using SmartOps.Domain.Modules.Timetable.Entities;

namespace SmartOps.Api.Modules.Timetable.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PeriodsController(
    IPeriodRepository periodRepository,
    IAuditLogRepository auditLogRepository) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = MenuPolicies.PeriodMaster.Add)]
    [ProducesResponseType(typeof(CreatePeriodResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreatePeriodResponse>> CreatePeriod([FromBody] CreatePeriodDto request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Period name is required.");
        }

        var id = await periodRepository.CreatePeriodAsync(request.ToEntity(), ct).ConfigureAwait(false);
        return Ok(new CreatePeriodResponse("Period created successfully", id));
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.PeriodMaster.View)]
    [ProducesResponseType(typeof(PagedResult<PeriodListModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPeriods(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? filter = "All",
        CancellationToken ct = default)
    {
        var result = await periodRepository
            .GetAllPeriodsAsync(pageIndex, pageSize, searchTerm, sortColumn, sortDirection, filter, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("/api/period/dropdown")]
    [Authorize(Policy = MenuPolicies.PeriodMaster.View)]
    [ProducesResponseType(typeof(IReadOnlyList<DropdownDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPeriodDropdown(CancellationToken ct)
    {
        var result = await periodRepository.GetPeriodDropdownAsync(ct).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.PeriodMaster.View)]
    [ProducesResponseType(typeof(PeriodEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PeriodEntity>> GetPeriodById(Guid id, CancellationToken ct)
    {
        var period = await periodRepository.GetPeriodByIdAsync(id, ct, includeInactive: true).ConfigureAwait(false);
        return period is null ? NotFound() : Ok(period);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.PeriodMaster.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdatePeriod(Guid id, [FromBody] CreatePeriodDto request, CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest("Period data is required.");
        }

        var existing = await periodRepository.GetPeriodByIdAsync(id, ct).ConfigureAwait(false);
        if (existing is null)
        {
            return NotFound();
        }

        var entity = request.ToEntity();
        entity.Id = id;
        entity.BranchId = existing.BranchId;
        entity.VersionNo = existing.VersionNo;
        entity.CreatedBy = existing.CreatedBy;
        entity.CreatedOn = existing.CreatedOn;

        await periodRepository.UpdatePeriodAsync(entity, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.PeriodMaster.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeletePeriod(Guid id, CancellationToken ct)
    {
        await periodRepository.DeletePeriodAsync(id, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.PeriodMaster.View)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await auditLogRepository.GetEntityHistoryAsync(
            DatabaseConfig.TablePeriods, id, page, pageSize, ct);

        return Ok(result);
    }
}
