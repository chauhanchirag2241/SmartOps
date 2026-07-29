using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Audit.Interfaces;
using SmartOps.Application.Modules.Shift;
using SmartOps.Domain.Common.Configuration;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Common.Models;
using SmartOps.Domain.Modules.Shift;
using SmartOps.Domain.Modules.Shift.Entities;

namespace SmartOps.Api.Modules.Shift.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ShiftsController(
    IShiftRepository shiftRepository,
    IAuditLogRepository auditLogRepository) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = MenuPolicies.Shifts.Add)]
    [ProducesResponseType(typeof(CreateShiftResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreateShiftResponse>> Create([FromBody] CreateShiftDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ShiftName))
        {
            return BadRequest("Shift name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.StartTime) || string.IsNullOrWhiteSpace(request.EndTime))
        {
            return BadRequest("Shift start and end time are required.");
        }

        var entity = request.ToEntity();
        var id = await shiftRepository.CreateAsync(entity, ct).ConfigureAwait(false);
        return Ok(new CreateShiftResponse("Shift created successfully", id));
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.Shifts.View)]
    [ProducesResponseType(typeof(PagedResult<ShiftListModel>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? sortColumn = null,
        [FromQuery] string? sortDirection = null,
        [FromQuery] string? filter = "All",
        CancellationToken ct = default)
    {
        var result = await shiftRepository
            .GetAllAsync(pageIndex, pageSize, searchTerm, sortColumn, sortDirection, filter, ct)
            .ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("/api/shift/dropdown")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<DropdownDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDropdown(CancellationToken ct)
    {
        var result = await shiftRepository.GetDropdownAsync(ct).ConfigureAwait(false);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Shifts.View)]
    [ProducesResponseType(typeof(ShiftEntity), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftEntity>> GetById(Guid id, CancellationToken ct)
    {
        var shift = await shiftRepository.GetByIdAsync(id, ct, includeInactive: true).ConfigureAwait(false);
        return shift is null ? NotFound() : Ok(shift);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Shifts.Edit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateShiftDto request, CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest("Shift data is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ShiftName))
        {
            return BadRequest("Shift name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.StartTime) || string.IsNullOrWhiteSpace(request.EndTime))
        {
            return BadRequest("Shift start and end time are required.");
        }

        var existing = await shiftRepository.GetByIdAsync(id, ct).ConfigureAwait(false);
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

        await shiftRepository.UpdateAsync(entity, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Shifts.Delete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await shiftRepository.DeleteAsync(id, ct).ConfigureAwait(false);
        return NoContent();
    }

    [HttpGet("{id:guid}/history")]
    [Authorize(Policy = MenuPolicies.Shifts.View)]
    public async Task<IActionResult> GetHistory(
        [FromRoute] Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var history = await auditLogRepository
            .GetEntityHistoryAsync(DatabaseConfig.TableShifts, id, page, pageSize, ct)
            .ConfigureAwait(false);
        return Ok(history);
    }
}
