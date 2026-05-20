using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Homework;
using SmartOps.Application.Modules.Homework.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Homework.Controllers;

[ApiController]
[Route("api/homework")]
[Authorize]
public sealed class HomeworkController : ControllerBase
{
    private readonly IHomeworkService _service;
    private readonly ILogger<HomeworkController> _logger;

    public HomeworkController(IHomeworkService service, ILogger<HomeworkController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.Homework.View)]
    [ProducesResponseType(typeof(IList<HomeworkListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? classId,
        [FromQuery] Guid? subjectId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await _service.GetListAsync(classId, subjectId, status, search, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("stats")]
    [Authorize(Policy = MenuPolicies.Homework.View)]
    [ProducesResponseType(typeof(HomeworkStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _service.GetStatsAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Homework.View)]
    [ProducesResponseType(typeof(HomeworkDetailResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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

    [HttpPost]
    [Authorize(Policy = MenuPolicies.Homework.Add)]
    [ProducesResponseType(typeof(HomeworkDetailResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CreateHomeworkRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Create homework failed: {Error}", result.Error);
        }

        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Homework.Edit)]
    [ProducesResponseType(typeof(HomeworkDetailResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHomeworkRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Homework.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/submit-submissions")]
    [Authorize(Policy = MenuPolicies.Homework.Edit)]
    [ProducesResponseType(typeof(HomeworkDetailResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitSubmissions(
        Guid id,
        [FromBody] SubmitHomeworkSubmissionsRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.SubmitSubmissionsAsync(id, request, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Submit homework submissions failed for {Id}: {Error}", id, result.Error);
        }

        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}/submissions")]
    [Authorize(Policy = MenuPolicies.Homework.Edit)]
    [ProducesResponseType(typeof(HomeworkDetailResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSubmissions(
        Guid id,
        [FromBody] UpdateHomeworkSubmissionsRequestDto request,
        CancellationToken ct)
    {
        var result = await _service.UpdateSubmissionsAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
