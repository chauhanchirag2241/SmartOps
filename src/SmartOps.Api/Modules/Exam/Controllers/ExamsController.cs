using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Exam;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Exam.Controllers;

[ApiController]
[Route("api/exams")]
[Authorize]
public sealed class ExamsController : ControllerBase
{
    private readonly IExamService _service;
    private readonly ILogger<ExamsController> _logger;

    public ExamsController(IExamService service, ILogger<ExamsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.Exams.View)]
    [ProducesResponseType(typeof(IList<ExamListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] Guid? groupId,
        [FromQuery] Guid? classId,
        [FromQuery] int? status,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await _service.GetExamsAsync(groupId, classId, status, search, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("stats")]
    [Authorize(Policy = MenuPolicies.Exams.View)]
    [ProducesResponseType(typeof(ExamStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _service.GetExamStatsAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Exams.View)]
    [ProducesResponseType(typeof(ExamDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetExamAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.Exams.Add)]
    [ProducesResponseType(typeof(ExamDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] SaveExamRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateExamAsync(request, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Create exam failed: {Error}", result.Error);
        }

        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Exams.Edit)]
    [ProducesResponseType(typeof(ExamDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveExamRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateExamAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = MenuPolicies.Exams.Edit)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateExamStatusRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateExamStatusAsync(id, request.Status, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Exams.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteExamAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
