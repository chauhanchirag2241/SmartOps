using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Leave;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Leave.Controllers;

[ApiController]
[Route("api/leave/staff")]
[Authorize]
public sealed class StaffLeaveController : ControllerBase
{
    private readonly ILeaveService _service;

    public StaffLeaveController(ILeaveService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = MenuPolicies.LeaveStaff.View)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? status,
        [FromQuery] Guid? employeeid,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var result = await _service.GetStaffListAsync(status, employeeid, from, to, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("approvers")]
    [Authorize(Policy = MenuPolicies.LeaveStaff.View)]
    public async Task<IActionResult> GetApprovers(CancellationToken ct)
    {
        var result = await _service.GetStaffApproversAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("mine")]
    [Authorize(Policy = MenuPolicies.LeaveStaff.View)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var result = await _service.GetStaffMineAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.LeaveStaff.View)]
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
    [Authorize(Policy = MenuPolicies.LeaveStaff.Add)]
    public async Task<IActionResult> Create([FromBody] CreateLeaveRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateStaffAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = MenuPolicies.LeaveStaff.Add)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var result = await _service.SubmitStaffAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = MenuPolicies.LeaveStaff.Edit)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _service.CancelAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
