using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Workflow;
using SmartOps.Application.Modules.Workflow.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Workflow.Controllers;

[ApiController]
[Route("api/my-actions")]
[Authorize]
public sealed class MyActionsController : ControllerBase
{
    private readonly IWorkflowService _service;

    public MyActionsController(IWorkflowService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = MenuPolicies.MyActions.View)]
    public async Task<IActionResult> GetList([FromQuery] short? itemType, [FromQuery] string? search, CancellationToken ct)
    {
        var result = await _service.GetMyActionsAsync(itemType, search, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("stats")]
    [Authorize(Policy = MenuPolicies.MyActions.View)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _service.GetStatsAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.MyActions.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetDetailAsync(id, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = MenuPolicies.MyActions.Edit)]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteMyActionRequestDto request, CancellationToken ct)
    {
        var result = await _service.CompleteAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
