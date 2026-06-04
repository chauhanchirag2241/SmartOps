using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Leave;
using SmartOps.Application.Modules.Leave.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Leave.Controllers;

[ApiController]
[Route("api/leave/students")]
[Authorize]
public sealed class StudentLeaveController : ControllerBase
{
    private readonly ILeaveService _service;

    public StudentLeaveController(ILeaveService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = MenuPolicies.LeaveStudent.View)]
    public async Task<IActionResult> GetList([FromQuery] string? status, [FromQuery] Guid? studentId, CancellationToken ct)
    {
        var result = await _service.GetStudentListAsync(status, studentId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("children")]
    [Authorize(Policy = MenuPolicies.LeaveStudent.View)]
    public async Task<IActionResult> GetChildren(CancellationToken ct)
    {
        var result = await _service.GetLinkedStudentsForParentAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("mine")]
    [Authorize(Policy = MenuPolicies.LeaveStudent.View)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var result = await _service.GetStudentMineAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.LeaveStudent.View)]
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
    [Authorize(Policy = MenuPolicies.LeaveStudent.Add)]
    public async Task<IActionResult> Create([FromBody] CreateStudentLeaveRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateStudentAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = MenuPolicies.LeaveStudent.Add)]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var result = await _service.SubmitStudentAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = MenuPolicies.LeaveStudent.Edit)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _service.CancelAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
