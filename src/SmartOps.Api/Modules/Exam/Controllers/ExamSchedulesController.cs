using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Exam;
using SmartOps.Application.Modules.Exam.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.Exam.Controllers;

[ApiController]
[Route("api/exam-schedules")]
[Authorize]
public sealed class ExamSchedulesController : ControllerBase
{
    private readonly IExamService _service;

    public ExamSchedulesController(IExamService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = MenuPolicies.ExamSchedule.View)]
    [ProducesResponseType(typeof(IList<ExamScheduleItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList([FromQuery] Guid? examId, [FromQuery] Guid? classId, CancellationToken ct)
    {
        var result = await _service.GetSchedulesAsync(examId, classId, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.ExamSchedule.Add)]
    [ProducesResponseType(typeof(ExamScheduleItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] SaveExamScheduleRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateScheduleAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.ExamSchedule.Edit)]
    [ProducesResponseType(typeof(ExamScheduleItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveExamScheduleRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateScheduleAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.ExamSchedule.Delete)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteScheduleAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }
}
