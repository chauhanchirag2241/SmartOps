using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Exam;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Exam.Controllers;

[ApiController]
[Route("api/exam-marks")]
[Authorize]
public sealed class ExamMarksController : ControllerBase
{
    private readonly IExamMarksService _service;
    private readonly ILogger<ExamMarksController> _logger;

    public ExamMarksController(IExamMarksService service, ILogger<ExamMarksController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("grid/{scheduleId:guid}")]
    [Authorize(Policy = MenuPolicies.ExamMarksEntry.View)]
    [ProducesResponseType(typeof(ExamMarksGridDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGrid(Guid scheduleId, CancellationToken ct)
    {
        var result = await _service.GetGridAsync(scheduleId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("subject-progress")]
    [Authorize(Policy = MenuPolicies.ExamMarksEntry.View)]
    [ProducesResponseType(typeof(IList<ExamSubjectProgressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubjectProgress(
        [FromQuery] Guid examId,
        [FromQuery] Guid classId,
        CancellationToken ct)
    {
        var result = await _service.GetSubjectProgressAsync(examId, classId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("save")]
    [Authorize(Policy = MenuPolicies.ExamMarksEntry.Add)]
    [ProducesResponseType(typeof(ExamMarksGridDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Save([FromBody] SaveExamMarksRequestDto request, CancellationToken ct)
    {
        var result = await _service.SaveMarksAsync(request, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Save exam marks failed for schedule {ScheduleId}: {Error}", request.ExamScheduleId, result.Error);
        }

        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
