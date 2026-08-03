using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Leave;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Leave.Controllers;

[ApiController]
[Route("api/leave/types")]
[Authorize]
public sealed class LeaveTypesController : ControllerBase
{
    private readonly ILeaveTypeService _service;

    public LeaveTypesController(ILeaveTypeService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = MenuPolicies.LeaveTypes.View)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var result = await _service.GetAllAsync(includeInactive, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    /// <summary>Active leave types for staff apply (LEAVE_STAFF permission).</summary>
    [HttpGet("active")]
    [Authorize(Policy = MenuPolicies.LeaveStaff.View)]
    public async Task<IActionResult> GetActive(CancellationToken ct = default)
    {
        var result = await _service.GetAllAsync(includeInactive: false, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.LeaveTypes.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.LeaveTypes.Add)]
    public async Task<IActionResult> Create([FromBody] CreateLeaveTypeDto request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.LeaveTypes.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLeaveTypeDto request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
